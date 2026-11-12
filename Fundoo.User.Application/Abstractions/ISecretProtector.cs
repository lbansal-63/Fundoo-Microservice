namespace Fundoo.User.Application.Abstractions;

public interface ISecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}