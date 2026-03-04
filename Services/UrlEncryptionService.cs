using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace UniCP.Services
{
    public class UrlEncryptionService : IUrlEncryptionService
    {
        // TODO: Move this key to appsettings.json or Azure KeyVault in production
        // 32 chars for 256-bit key
        private readonly string _key = "UniveraCP_Secure_Key_2026_Trx9sZ"; 
        
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            try 
            {
                byte[] iv = new byte[16];
                byte[] array;

                using (Aes aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(_key);
                    aes.IV = iv;

                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                            {
                                streamWriter.Write(plainText);
                            }

                            array = memoryStream.ToArray();
                        }
                    }
                }

                // Convert to Base64 and URL-encode
                string base64 = Convert.ToBase64String(array);
                // We use HttpUtility or just classic Replace for URL safety if needed, 
                // but Base64 might contain + and / which are messy in URLs.
                // Let's make it URL safe manually or use WebEncoders.
                // Simple approach: Replace + with -, / with _ and remove =
                
                return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
            }
            catch
            {
                return plainText; 
            }
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                // Revert URL safe replacements
                string incoming = cipherText.Replace("-", "+").Replace("_", "/");
                switch (incoming.Length % 4)
                {
                    case 2: incoming += "=="; break;
                    case 3: incoming += "="; break;
                }

                byte[] iv = new byte[16];
                byte[] buffer = Convert.FromBase64String(incoming);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(_key);
                    aes.IV = iv;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream(buffer))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Decoding failed (tampered or invalid)
                return null;
            }
        }

        public string? EncryptId(int id)
        {
            return Encrypt(id.ToString());
        }

        public int? DecryptId(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return null;
            
            var decrypted = Decrypt(cipherText);
            if (int.TryParse(decrypted, out int result))
            {
                return result;
            }
            return null;
        }
    }
}
