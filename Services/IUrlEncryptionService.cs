namespace SOS.Services
{
    public interface IUrlEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
        string? EncryptId(int id);
        int? DecryptId(string cipherText);
    }
}

