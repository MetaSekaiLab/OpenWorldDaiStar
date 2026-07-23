using MessagePack;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Tests;

public sealed class ProtocolShapeTests
{
    [Theory]
    [InlineData(typeof(EnvironmentResult), 13)]
    [InlineData(typeof(MasterDataManifest), 4)]
    [InlineData(typeof(Fault), 3)]
    [InlineData(typeof(RegisterPayload), 1)]
    [InlineData(typeof(AccountRegistResult), 2)]
    [InlineData(typeof(AuthenticatePayload), 5)]
    [InlineData(typeof(AuthenticateResult), 3)]
    [InlineData(typeof(LoginPayload), 1)]
    [InlineData(typeof(LoginResult), 7)]
    public void GeneratedModel_UsesContiguousStubKeys(Type type, int keyCount)
    {
        var keys = type.GetProperties()
            .Select(property => property.GetCustomAttributes(typeof(KeyAttribute), false)
                .Cast<KeyAttribute>().Single().IntKey
                ?? throw new InvalidDataException($"{type.Name}.{property.Name} does not use an integer Key."))
            .Order()
            .ToArray();

        Assert.Equal(Enumerable.Range(0, keyCount), keys);
        Assert.NotNull(type.GetCustomAttributes(typeof(MessagePackObjectAttribute), false).Single());
    }

    [Fact]
    public void IDataObject_UnionOneUsesPlayerUserProfileFromStubImport()
    {
        var union = typeof(IDataObject).GetCustomAttributes(typeof(UnionAttribute), false)
            .Cast<UnionAttribute>()
            .Single(attribute => attribute.Key == 1);

        Assert.Equal(typeof(SiriusApi.Shared.Models.Player.UserProfile), union.SubType);
    }
}
