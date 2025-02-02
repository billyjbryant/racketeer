﻿using System;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Security;

namespace FileConnectorCommon
{
    public class KeyMgmt
    {

        public static string SecStr2Str(SecureString secStr)
        {
            try
            {
                return new System.Net.NetworkCredential(string.Empty, secStr).Password;

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                return "";
            }
        }
        public static byte[] GenerateRandomSalt()
        {
            byte[] data = new byte[32];

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                for (int i = 0; i < 10; i++)
                {
                    // Fille the buffer with the generated data
                    rng.GetBytes(data);
                }
            }

            return data;
        }


        public static SecureString GetSafeConsolePassword(String preamble)
        {
            SecureString password = new SecureString();
            Console.Write(preamble);

            ConsoleKeyInfo nextKey = Console.ReadKey(true);

            while (nextKey.Key != ConsoleKey.Enter)
            {
                if (nextKey.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.RemoveAt(password.Length - 1);
                        // erase the last * as well
                        Console.Write(nextKey.KeyChar);
                        Console.Write(" ");
                        Console.Write(nextKey.KeyChar);
                    }
                }
                else
                {
                    password.AppendChar(nextKey.KeyChar);
                    Console.Write("*");
                }
                nextKey = Console.ReadKey(true);
            }
            password.MakeReadOnly();
            return password;
        }

    }
    public static class SecurePasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 20;

        public static string Hash(string password, string siteId, int iterations)
        {
            // Create salt
            byte[] salt = new byte[SaltSize];
            Guid? siteGUID = null;

            try
            {
                siteGUID = new Guid(siteId);
            }
            catch (Exception e)
            {
                Console.WriteLine("\n Error in creating GUID from string: {0}", e.Message);
            }
            Console.WriteLine("\n{0}", siteGUID.ToString());
            salt = siteGUID?.ToByteArray();
            // new RNGCryptoServiceProvider().GetBytes(salt);

            // Create hash
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations);
            var hash = pbkdf2.GetBytes(HashSize);

            // Combine salt and hash
            var hashBytes = new byte[SaltSize + HashSize];
            Array.Copy(salt, 0, hashBytes, 0, SaltSize);
            Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

            // Convert to base64
            var base64Hash = Convert.ToBase64String(hashBytes);

            // Format hash with extra information
            return string.Format("$RNS$V1${0}${1}", iterations, base64Hash);
        }

        public static string Hash(string password, string salt)
        {
            return Hash(password, salt, 10000);
        }

        public static bool IsHashSupported(string hashString)
        {
            return hashString.Contains("$RNS$V1$");
        }

        public static bool Verify(string password, string hashedPassword)
        {
            // Check hash
            if (!IsHashSupported(hashedPassword))
            {
                throw new NotSupportedException("The hashtype is not supported");
            }

            // Extract iteration and Base64 string
            var splittedHashString = hashedPassword.Replace("$RNS$V1$", "").Split('$');
            var iterations = int.Parse(splittedHashString[0]);
            var base64Hash = splittedHashString[1];

            // Get hash bytes
            var hashBytes = Convert.FromBase64String(base64Hash);

            // Get salt
            var salt = new byte[SaltSize];
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);

            // Create hash with given salt
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations);
            byte[] hash = pbkdf2.GetBytes(HashSize);

            // Get result
            for (var i = 0; i < HashSize; i++)
            {
                if (hashBytes[i + SaltSize] != hash[i])
                {
                    hashBytes = null;
                    return false;
                }
            }
            return true;
        }
    }

    public static class SecurePwEncryptor
    {
        public static string EncryptString(string key, string plainInput, string siteID)
        {
            byte[] iv = new byte[16];
            byte[] array;
            using (Aes aes = Aes.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(key);
                byte[] salt = new byte[16];
                salt = Encoding.UTF8.GetBytes(siteID);
                var keyDer = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
                aes.Key = keyDer.GetBytes(32);
                aes.IV = iv;
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainInput);
                        }

                        array = memoryStream.ToArray();
                    }
                }
            }

            return Convert.ToBase64String(array);
        }

        public static string DecryptString(string key, string cipherText, string siteID)
        {
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(cipherText);
            using (Aes aes = Aes.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(key);
                byte[] salt = new byte[16];
                salt = Encoding.UTF8.GetBytes(siteID);
                var keyDer = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
                aes.Key = keyDer.GetBytes(32);
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
    }
}
