using Eventuous.Projections.MongoDB;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using MongoDB.Driver;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Projections.MongoDB;

[ClassDataSource<IntegrationFixture>]
public class ProjectWithBulkBuilder(IntegrationFixture fixture) : ProjectionTestBase<ProjectWithBulkBuilder.SutBulkProjection>(nameof(ProjectWithBulkBuilder), fixture) {
    [Test]
    public async Task ShouldProjectImported() {
        await InitializeAsync();
        var evt    = DomainFixture.CreateImportBookingEvent();
        var id     = new BookingId(CreateId());
        var stream = StreamNameFactory.For<Booking, BookingState, BookingId>(id);

        var first = await Act(stream, evt);

        var expected = new BookingDocument(id.ToString()) {
            RoomId         = evt.RoomId,
            CheckInDate    = evt.CheckIn,
            CheckOutDate   = evt.CheckOut,
            BookingPrice   = evt.Price,
            Outstanding    = evt.Price,
            Position       = first.Append.GlobalPosition,
            StreamPosition = (ulong)first.Append.NextExpectedVersion
        };

        first.Doc.Should().BeEquivalentTo(expected);

        var payment = new BookingPaymentRegistered(Guid.NewGuid().ToString(), evt.Price);

        var second = await Act(stream, payment);
        await DisposeAsync();

        expected = expected with {
            PaidAmount = payment.AmountPaid,
            Position = second.Append.GlobalPosition,
            StreamPosition = (ulong)second.Append.NextExpectedVersion
        };

        second.Doc.Should().BeEquivalentTo(expected);
    }

    async Task<(AppendEventsResult Append, BookingDocument? Doc)> Act<T>(StreamName stream, T evt)
        where T : class {
        var append = await Fixture.AppendEvent(stream, evt);
        await WaitForPosition(append.GlobalPosition);
        var actual = await Fixture.Mongo.LoadDocument<BookingDocument>(stream.GetId());

        return (append, actual);
    }

    public class SutBulkProjection : MongoProjector<BookingDocument> {
        public SutBulkProjection(IMongoDatabase database)
            : base(database) {
            On<BookingImported>(
                b => b
                    .Bulk
                    .AddOperation(
                        x => x.InsertOne
                            .Document(
                                ctx => new(ctx.Stream.GetId()) {
                                    RoomId       = ctx.Message.RoomId,
                                    CheckInDate  = ctx.Message.CheckIn,
                                    CheckOutDate = ctx.Message.CheckOut,
                                    BookingPrice = ctx.Message.Price,
                                    Outstanding  = ctx.Message.Price
                                }
                            )
                    )
            );

            On<RoomBooked>(
                b => b
                    .Bulk
                    .AddOperation(
                        x => x.InsertOne
                            .Document(
                                ctx => new(ctx.Stream.GetId()) {
                                    BookingPrice = ctx.Message.Price,
                                    Outstanding  = ctx.Message.Price
                                }
                            )
                    )
            );

            On<BookingPaymentRegistered>(
                b => b
                    .Bulk
                    .AddOperation(
                        x => x.UpdateOne
                            .DefaultId()
                            .Update((evt, update) => update.Set(d => d.PaidAmount, evt.AmountPaid))
                    )
            );
        }
    }
}
