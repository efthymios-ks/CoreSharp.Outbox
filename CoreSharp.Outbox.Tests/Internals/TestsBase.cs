using AutoFixture.AutoNSubstitute;

namespace CoreSharp.Outbox.Tests.Internals;

public abstract class TestsBase
{
    private readonly IFixture _fixture;

    protected TestsBase()
    {
        _fixture = new Fixture();
        _fixture.Customize(new AutoNSubstituteCustomization
        {
            ConfigureMembers = true,
            GenerateDelegates = true,
        });

        ConfigureFixture(_fixture);
    }

    protected virtual void ConfigureFixture(IFixture fixture)
    {
    }

    protected TElement MockCreate<TElement>()
        => _fixture.Create<TElement>();

    protected IEnumerable<TElement> MockCreateMany<TElement>()
        => _fixture.CreateMany<TElement>();

    protected IEnumerable<TElement> MockCreateMany<TElement>(int count)
        => _fixture.CreateMany<TElement>(count);

    protected TElement MockFreeze<TElement>()
        => _fixture.Freeze<TElement>();
}
