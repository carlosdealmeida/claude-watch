using ClaudeWatch.Credentials;
using Xunit;

public class TokenSelectorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1000);
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(2);
    private static OAuthCredential C(string t, int mins) => new(t, null, Now.AddMinutes(mins));

    [Fact] public void Escolhe_o_mais_fresco() =>
        Assert.Equal("cache", TokenSelector.PickValid(C("file", 10), C("cache", 60), Now, Margin)!.AccessToken);

    [Fact] public void Margem_invalida_token_quase_expirado() =>
        Assert.Null(TokenSelector.PickValid(C("file", 1), null, Now, Margin));

    [Fact] public void Ambos_expirados_retorna_null() =>
        Assert.Null(TokenSelector.PickValid(C("a", -5), C("b", -1), Now, Margin));
}
