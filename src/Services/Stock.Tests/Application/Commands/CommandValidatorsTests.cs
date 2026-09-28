using FluentValidation;
using FluentValidation.Results;
using Stock.Application.Commands.ChangeStockName;
using Stock.Application.Commands.ChangeStockTradingTime;
using Stock.Application.Commands.CreateStock;
using Stock.Application.Commands.RequestHourlyReadModel;
using Stock.Application.Commands.UpdateNameReadModel;
using Stock.Application.Commands.UpdateTradingTimeReadModel;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class CommandValidatorsTests
{
    private static readonly CreateStockCommand ValidCreateStock =
        new(Guid.NewGuid(), new string('A', 20), true, "USD", new TimeOnly(9, 0), new TimeOnly(17, 0));

    public static TheoryData<CreateStockCommand, string> InvalidCreateStockCommands => new()
    {
        { ValidCreateStock with { Id = Guid.Empty }, "Id" },
        { ValidCreateStock with { Name = "" }, "Name" },
        { ValidCreateStock with { Name = new string('A', 21) }, "Name" },
        { ValidCreateStock with { Currency = "usd" }, "Currency" },
        { ValidCreateStock with { Currency = "US" }, "Currency" },
        { ValidCreateStock with { Currency = "USDT" }, "Currency" },
        { ValidCreateStock with { Currency = "U1D" }, "Currency" },
        { ValidCreateStock with { TradingEndTime = new TimeOnly(9, 0) }, "TradingEndTime" },
        { ValidCreateStock with { TradingEndTime = new TimeOnly(8, 0) }, "TradingEndTime" }
    };

    [Fact]
    public void CreateStock_AcceptsValidCommand()
    {
        Assert.True(new CreateStockValidator().Validate(ValidCreateStock).IsValid);
    }

    [Theory]
    [MemberData(nameof(InvalidCreateStockCommands))]
    public void CreateStock_RejectsInvalidField(CreateStockCommand command, string property)
    {
        AssertInvalid(new CreateStockValidator(), command, property);
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA", "Name")]
    public void ChangeStockName_RejectsInvalidName(string name, string property)
    {
        AssertInvalid(new ChangeStockNameValidator(), new ChangeStockNameCommand(Guid.NewGuid(), name), property);
    }

    [Fact]
    public void ChangeStockName_AcceptsNameAtMaxLength_AndRejectsEmptyId()
    {
        Assert.True(new ChangeStockNameValidator()
            .Validate(new ChangeStockNameCommand(Guid.NewGuid(), new string('A', 20))).IsValid);
        AssertInvalid(new ChangeStockNameValidator(), new ChangeStockNameCommand(Guid.Empty, "Name"), "Id");
    }

    [Theory]
    [InlineData(9, 17, true)]
    [InlineData(9, 9, false)]
    [InlineData(17, 9, false)]
    public void ChangeStockTradingTime_RequiresCloseAfterOpen(int openHour, int closeHour, bool valid)
    {
        var result = new ChangeStockTradingTimeValidator().Validate(
            new ChangeStockTradingTimeCommand(Guid.NewGuid(), new TimeOnly(openHour, 0), new TimeOnly(closeHour, 0)));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("Name", 1, true, "")]
    [InlineData("Name", 0, false, "Version")]
    [InlineData("", 1, false, "Name")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA", 1, false, "Name")]
    public void UpdateNameReadModel_ValidatesNameAndVersion(string name, long version, bool valid, string property)
    {
        var command = new UpdateNameReadModelCommand(Guid.NewGuid(), name, version);

        if (valid) Assert.True(new UpdateNameReadModelValidator().Validate(command).IsValid);
        else AssertInvalid(new UpdateNameReadModelValidator(), command, property);
    }

    [Theory]
    [InlineData(9, 17, 1, true, "")]
    [InlineData(9, 9, 1, false, "TradingCloseTime")]
    [InlineData(9, 17, 0, false, "Version")]
    public void UpdateTradingTime_ValidatesTimesAndVersion(int startHour, int closeHour, long version, bool valid,
        string property)
    {
        var command = new UpdateTradingTimeCommand(Guid.NewGuid(), new TimeOnly(startHour, 0),
            new TimeOnly(closeHour, 0), version);

        if (valid) Assert.True(new UpdateTradingTimeValidator().Validate(command).IsValid);
        else AssertInvalid(new UpdateTradingTimeValidator(), command, property);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(23, true)]
    [InlineData(24, false)]
    public void RequestHourlyReadModel_AcceptsHoursUpTo23(byte hour, bool valid)
    {
        var result = new RequestHourlyReadModelValidator()
            .Validate(new RequestHourlyReadModelCommand(new DateOnly(2026, 3, 5), hour));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void RequestHourlyReadModel_RejectsDefaultDate()
    {
        AssertInvalid(new RequestHourlyReadModelValidator(), new RequestHourlyReadModelCommand(default, 10), "Date");
    }

    private static void AssertInvalid<T>(IValidator<T> validator, T command, string property)
    {
        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }
}
