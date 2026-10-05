using System.ComponentModel.DataAnnotations;
using AeroCompliance.Contracts.Dtos.Auth;

namespace AeroCompliance.UnitTests.Authentication;

public class AuthDtoValidationTests
{
    [Fact]
    public void Login_rejects_missing_email()
        => AssertInvalid(new LoginRequest(null!, "ValidPassword123!"), nameof(LoginRequest.Email));

    [Fact]
    public void Login_rejects_invalid_email()
        => AssertInvalid(new LoginRequest("not-an-email", "ValidPassword123!"), nameof(LoginRequest.Email));

    [Fact]
    public void Login_rejects_missing_password()
        => AssertInvalid(new LoginRequest("person@example.test", "   "), nameof(LoginRequest.Password));

    [Fact]
    public void Refresh_rejects_empty_token()
        => AssertInvalid(new RefreshTokenRequest(" "), nameof(RefreshTokenRequest.RefreshToken));

    [Fact]
    public void Logout_rejects_empty_token()
        => AssertInvalid(new LogoutRequest(string.Empty), nameof(LogoutRequest.RefreshToken));

    private static void AssertInvalid(object value, string memberName)
    {
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(value, new ValidationContext(value), results, true);

        Assert.False(valid);
        Assert.Contains(results, result => result.MemberNames.Contains(memberName));
    }
}
