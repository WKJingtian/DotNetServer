using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EncryptHelper
{
    /// <summary>
    /// 加解密帮助类
    /// </summary>
    public class EncryptHelper
    {
        private static Dictionary<string, List<(int, int)>> confuseDic = new Dictionary<string, List<(int, int)>>()
        {
            {"0", new () {(23, 51),(45, 47),(10, 57),(11, 38),(5, 8),(19, 54),(27, 20),(2, 30),(51, 37),(17, 40),(42, 33),(12, 7),(57, 39),(3, 28),(21, 35),(36, 58),(44, 21),(38, 24),(9, 17),(46, 3),(22, 31),(52, 60),(58, 34),(41, 55),(32, 6),(48, 5),(49, 42),(28, 1),(33, 46),(0, 16),}},
            {"1", new () {(45, 3),(10, 41),(40, 0),(57, 20),(4, 28),(41, 42),(32, 19),(23, 27),(11, 25),(42, 23),(20, 38),(9, 34),(18, 33),(6, 24),(16, 14),(15, 52),(12, 31),(2, 48),(1, 55),(8, 60),(29, 36),(48, 22),(53, 8),(34, 1),(59, 21),(44, 17),(13, 39),(55, 7),(27, 18),(5, 43),}},
            {"2", new () {(23, 36),(61, 42),(6, 12),(14, 28),(18, 31),(48, 58),(50, 0),(43, 37),(47, 4),(11, 1),(57, 32),(12, 55),(45, 47),(19, 53),(58, 14),(35, 50),(29, 7),(41, 40),(2, 25),(32, 21),(51, 34),(1, 39),(33, 3),(37, 2),(30, 60),(52, 17),(16, 18),(59, 8),(56, 44),(10, 5),}},
            {"3", new () {(38, 43),(59, 20),(10, 3),(8, 61),(32, 57),(12, 55),(56, 42),(62, 34),(51, 50),(41, 23),(23, 45),(49, 39),(1, 28),(27, 24),(37, 11),(35, 21),(25, 59),(2, 54),(21, 52),(0, 18),(6, 13),(44, 46),(57, 14),(58, 41),(40, 53),(22, 56),(36, 40),(18, 8),(26, 30),(15, 47),}},
            {"4", new () {(26, 12),(28, 20),(45, 1),(62, 21),(22, 43),(60, 8),(9, 3),(44, 34),(32, 58),(43, 7),(50, 36),(29, 50),(36, 29),(49, 26),(7, 37),(33, 62),(41, 39),(30, 52),(18, 28),(47, 54),(51, 0),(1, 59),(57, 30),(54, 49),(61, 40),(2, 24),(16, 48),(55, 57),(59, 56),(3, 25),}},
            {"5", new () {(48, 7),(44, 23),(37, 50),(52, 39),(9, 10),(42, 33),(56, 51),(54, 27),(26, 47),(25, 43),(32, 26),(49, 53),(43, 20),(10, 19),(8, 56),(24, 11),(31, 62),(55, 38),(27, 31),(40, 58),(62, 61),(59, 9),(47, 48),(29, 59),(21, 13),(46, 25),(1, 46),(45, 52),(7, 40),(58, 45),}},
            {"6", new () {(8, 57),(3, 59),(5, 3),(18, 62),(59, 54),(43, 34),(47, 9),(44, 30),(50, 4),(57, 36),(34, 32),(22, 0),(9, 48),(17, 2),(1, 28),(42, 31),(40, 21),(11, 43),(32, 14),(58, 22),(62, 17),(6, 5),(54, 33),(60, 10),(19, 44),(27, 53),(55, 6),(29, 12),(20, 50),(46, 47),}},
            {"7", new () {(48, 47),(57, 52),(13, 20),(54, 58),(35, 21),(37, 24),(5, 10),(51, 50),(23, 37),(28, 43),(20, 35),(46, 25),(31, 22),(10, 26),(38, 16),(40, 29),(19, 12),(18, 45),(9, 46),(2, 59),(41, 13),(8, 1),(7, 11),(55, 44),(11, 36),(3, 6),(50, 30),(22, 38),(12, 7),(0, 34),}},
            {"8", new () {(23, 54),(52, 26),(42, 9),(18, 52),(13, 44),(43, 27),(11, 0),(60, 15),(58, 14),(46, 29),(32, 34),(7, 55),(37, 7),(29, 28),(4, 3),(27, 13),(61, 25),(3, 5),(49, 59),(39, 45),(51, 2),(16, 50),(38, 60),(8, 12),(14, 10),(6, 43),(24, 11),(59, 62),(20, 1),(26, 53),}},
            {"9", new () {(37, 15),(23, 25),(21, 33),(36, 34),(2, 21),(6, 8),(27, 48),(39, 52),(58, 53),(20, 51),(52, 61),(3, 17),(43, 58),(38, 46),(4, 5),(42, 23),(61, 19),(48, 39),(30, 35),(31, 40),(62, 27),(25, 30),(10, 43),(55, 6),(46, 57),(15, 24),(0, 26),(28, 55),(50, 28),(41, 50),}},
            {"A", new () {(56, 33),(44, 26),(53, 19),(1, 60),(8, 6),(46, 62),(28, 1),(49, 55),(37, 25),(36, 30),(29, 24),(25, 11),(27, 28),(4, 16),(60, 20),(55, 29),(33, 47),(10, 48),(62, 52),(9, 31),(6, 57),(38, 15),(41, 54),(40, 2),(58, 51),(19, 56),(3, 34),(34, 36),(17, 59),(54, 13),}},
            {"B", new () {(6, 33),(9, 7),(37, 4),(52, 24),(1, 48),(12, 17),(20, 44),(3, 34),(2, 13),(60, 28),(38, 35),(45, 39),(25, 36),(41, 45),(42, 51),(27, 55),(30, 31),(62, 2),(19, 29),(39, 50),(36, 59),(15, 52),(50, 30),(5, 46),(44, 60),(21, 20),(33, 14),(22, 49),(55, 6),(53, 11),}},
            {"C", new () {(49, 26),(50, 38),(28, 3),(62, 30),(41, 33),(57, 35),(59, 50),(26, 15),(37, 61),(19, 46),(32, 9),(16, 11),(47, 17),(40, 22),(55, 31),(24, 8),(20, 18),(30, 57),(33, 20),(42, 12),(58, 14),(46, 25),(13, 44),(23, 47),(21, 56),(29, 39),(61, 37),(39, 19),(14, 29),(9, 16),}},
            {"D", new () {(31, 41),(10, 7),(0, 37),(29, 31),(33, 4),(22, 50),(34, 53),(6, 56),(44, 54),(59, 60),(46, 10),(4, 3),(13, 51),(12, 46),(61, 44),(60, 38),(43, 48),(9, 17),(2, 45),(3, 12),(35, 8),(45, 13),(32, 35),(18, 6),(1, 0),(5, 18),(48, 39),(37, 40),(26, 16),(21, 36),}},
            {"E", new () {(28, 15),(4, 19),(61, 34),(59, 22),(55, 23),(17, 0),(11, 61),(12, 57),(21, 53),(25, 18),(13, 50),(29, 17),(14, 26),(8, 33),(7, 49),(5, 14),(58, 62),(36, 12),(46, 16),(48, 32),(52, 24),(31, 13),(50, 59),(60, 29),(10, 31),(34, 3),(20, 8),(3, 46),(37, 40),(45, 41),}},
            {"F", new () {(16, 53),(50, 18),(9, 17),(8, 0),(40, 21),(32, 27),(18, 48),(17, 14),(31, 10),(52, 23),(61, 50),(53, 4),(29, 57),(55, 6),(24, 16),(28, 32),(15, 37),(48, 3),(25, 59),(10, 39),(14, 45),(44, 62),(1, 52),(20, 7),(35, 54),(19, 25),(42, 2),(39, 56),(51, 30),(34, 13),}},
        };
        
        public static string CustomHash(string content)
        {
            var contentHash = SHA1(content + "jf&639JhsHG920.;266sGG8");
            for (var i = 0; i < 3; i++)
                contentHash = MD5Encrypt(content + "7sh^792Hgs902*&62");
            for (var i = 0; i < 2; i++)
                contentHash = SHA1(content + "739))2jh^673290/'o8");
            for (var i = 0; i < 6; i++)
                contentHash = MD5Encrypt(content + "6GGjsi23*72.$723(86");
            for (var i = 0; i < 1; i++)
                contentHash = SHA1(content + "847903mJhs7&9092,.]2917");
            return contentHash;
        }

        public static string CustomDeConfuse(string content)
        {
            var first = content[..1];
            var rest = content[1..];
            var charArr = rest.ToCharArray();
            var pairList = confuseDic[first];
            for (var i = pairList.Count - 1 ; i >= 0; --i)
            {
                var pair = pairList[i];
                (charArr[pair.Item1], charArr[pair.Item2]) = (charArr[pair.Item2], charArr[pair.Item1]);
            }
            return new string(charArr[..40]);
        }
        
        //DES用于加密内容较多的敏感信息
        //AES用于加密内容较少强度较高的信息

        /// <summary>
        /// DES加密方法
        /// </summary>
        /// <param name="value">待加密的字符串</param>
        /// <param name="key">8/16位密钥</param>
        /// <returns></returns>
        public static string DesEncrypt(string value, string key)
        {
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            byte[] inputByteArray = Encoding.Default.GetBytes(value);
            des.Key = Encoding.ASCII.GetBytes(key);
            des.IV = Encoding.ASCII.GetBytes(key);
            //创建其支持存储区为内存的流
            MemoryStream ms = new MemoryStream();
            //将数据流链接到加密转换的流
            CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(inputByteArray, 0, inputByteArray.Length);
            //用缓冲区的当前状态更新基础数据源或储存库，随后清除缓冲区
            cs.FlushFinalBlock();
            byte[] EncryptData = ms.ToArray();
            return Convert.ToBase64String(EncryptData, 0, EncryptData.Length);
        }
        /// <summary>
        /// DES解密方法
        /// </summary>
        /// <param name="value">需要解密的字符串</param>
        /// <param name="key">密钥</param>
        /// <returns></returns> 
        public static string DesDecrypt(string value, string key)
        {
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            //Put  the  input  string  into  the  byte  array 
            byte[] inputByteArray = Convert.FromBase64String(value);
            //建立加密对象的密钥和偏移量
            des.Key = Encoding.ASCII.GetBytes(key);
            des.IV = Encoding.ASCII.GetBytes(key);
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write);
            //Flush  the  data  through  the  crypto  stream  into  the  memory  stream 
            cs.Write(inputByteArray, 0, inputByteArray.Length);
            cs.FlushFinalBlock();
            return Encoding.Default.GetString(ms.ToArray());
        }
        /// <summary>
        /// Aes加密
        /// </summary>
        /// <param name="value">源字符串</param>
        /// <param name="key">aes密钥，长度必须32位</param>
        /// <returns>加密后的字符串</returns>
        public static string AESEncrypt(string value, string key)
        {
            using (AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider())
            {
                aesProvider.Key = Encoding.UTF8.GetBytes(key);
                aesProvider.Mode = CipherMode.ECB;
                aesProvider.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform cryptoTransform = aesProvider.CreateEncryptor())
                {
                    byte[] inputBuffers = Encoding.UTF8.GetBytes(value);
                    byte[] results = cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length);
                    aesProvider.Clear();
                    aesProvider.Dispose();
                    return Convert.ToBase64String(results, 0, results.Length);
                }
            }
        }
        /// <summary>
        /// Aes解密
        /// </summary>
        /// <param name="value">源字符串</param>
        /// <param name="key">aes密钥，长度必须32位</param>
        /// <returns>解密后的字符串</returns>
        public static string AESDecrypt(string value, string key)
        {
            using (AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider())
            {
                aesProvider.Key = Encoding.UTF8.GetBytes(key);
                aesProvider.Mode = CipherMode.ECB;
                aesProvider.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform cryptoTransform = aesProvider.CreateDecryptor())
                {
                    byte[] inputBuffers = Convert.FromBase64String(value);
                    byte[] results = cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length);
                    aesProvider.Clear();
                    return Encoding.UTF8.GetString(results);
                }
            }
        }
        /// <summary>
        /// MD5加密
        /// </summary>
        /// <param name="value">需要加密字符串</param>
        /// <returns>返回32位大写字符</returns>
        public static string MD5Encrypt(string value)
        {
            //将输入字符串转换成字节数组  ANSI代码页编码
            var buffer = Encoding.Default.GetBytes(value);
            //接着，创建Md5对象进行散列计算
            var data = MD5.Create().ComputeHash(buffer);
            //创建一个新的Stringbuilder收集字节
            var sb = new StringBuilder();
            //遍历每个字节的散列数据 
            foreach (var t in data)
            {
                //转换大写十六进制字符串
                sb.Append(t.ToString("X2"));
            }
            //返回十六进制字符串
            return sb.ToString();
        }
        /// <summary>  
        /// SHA1加密
        /// </summary>  
        /// <param name="value">需要加密字符串</param>
        /// <returns>返回40位大写字符串</returns>  
        public static string SHA1(string value)
        {
            //UTF8编码
            var buffer = Encoding.UTF8.GetBytes(value);
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            var data = sha1.ComputeHash(buffer);
            var sb = new StringBuilder();
            foreach (var t in data)
            {
                //转换大写十六进制
                sb.Append(t.ToString("X2"));
            }
            return sb.ToString();
        }
        /// <summary>
        /// Base64编码
        /// </summary>
        /// <param name="code_type"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        public static string EncodeBase64(string code_type, string code)
        {
            string encode = "";
            byte[] bytes = Encoding.GetEncoding(code_type).GetBytes(code);
            try
            {
                encode = Convert.ToBase64String(bytes);
            }
            catch
            {
                encode = code;
            }
            return encode;
        }
        /// <summary>
        /// Base64解码
        /// </summary>
        /// <param name="code_type"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        public static string DecodeBase64(string code_type, string code)
        {
            string decode = "";
            byte[] bytes = Convert.FromBase64String(code);
            try
            {
                decode = Encoding.GetEncoding(code_type).GetString(bytes);
            }
            catch
            {
                decode = code;
            }
            return decode;
        }
        /// <summary>
        /// SQL注入字符清理
        /// </summary>
        /// <param name="value">需要清理的字符串</param>
        /// <returns></returns>
        public static string SqlTextClear(string value)
        {
            string[] replaceStr = new string[] { ",", "<", ">", "--", "'", "\"", "=", "%", " " };
            foreach (var item in replaceStr)
            {
                value = value.Replace(item, "");
            }
            return value;
        }
        /// <summary>
        /// 替换特殊字符，防SQL注入
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ReplaceSQLChar(string str)
        {
            if (string.IsNullOrEmpty(str))
                return "";

            str = str.Replace("'", "");
            str = str.Replace(";", "");
            str = str.Replace(",", "");
            str = str.Replace("?", "");
            str = str.Replace("<", "");
            str = str.Replace(">", "");
            str = str.Replace("(", "");
            str = str.Replace(")", "");
            str = str.Replace("@", "");
            str = str.Replace("=", "");
            str = str.Replace("+", "");
            str = str.Replace("*", "");
            str = str.Replace("&", "");
            str = str.Replace("#", "");
            str = str.Replace("%", "");
            str = str.Replace("$", "");

            //删除与数据库相关的词
            str = Regex.Replace(str, "select", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "insert", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "delete from", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "count", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "drop table", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "truncate", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "asc", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "mid", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "char", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "xp_cmdshell", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "exec master", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "net localgroup administrators", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "and", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "net user", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "or", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "net", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "-", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "delete", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "drop", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "script", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "update", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "and", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "chr", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "master", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "truncate", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "declare", "", RegexOptions.IgnoreCase);
            str = Regex.Replace(str, "mid", "", RegexOptions.IgnoreCase);

            return str;
        }
        
        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+{}[]<>|";
            StringBuilder stringBuilder = new StringBuilder(length);
            var random = new Random();

            for (int i = 0; i < length; i++)
            {
                int index = random.Next(chars.Length);
                stringBuilder.Append(chars[index]);
            }

            return stringBuilder.ToString();
        }    
    }
}