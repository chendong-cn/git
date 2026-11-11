using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NetConfigPro.Services;

public static class AppStorage
{
    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetConfigPro");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>本次进程内读取数据文件时发现的恢复事件（损坏隔离），供启动时向用户提示。</summary>
    public static List<string> RecoveryNotes { get; } = new();

    static AppStorage()
    {
        Directory.CreateDirectory(DataDir);
    }

    public static string PathOf(string fileName) => Path.Combine(DataDir, fileName);

    public static List<T> Load<T>(string fileName)
    {
        var path = PathOf(fileName);
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? [];
        }
        catch
        {
            // 数据文件损坏时隔离备份，避免应用无法启动；同时记录恢复事件供启动时提示用户
            var bak = path + ".bak." + DateTime.Now.ToString("yyyyMMddHHmmss");
            try
            {
                File.Move(path, bak, true);
                RecoveryNotes.Add($"「{fileName}」读取失败，原文件已隔离为 {Path.GetFileName(bak)}——该模块数据已重置，如需找回请用备份文件");
            }
            catch
            {
                RecoveryNotes.Add($"「{fileName}」读取失败且备份也失败，该模块数据已重置");
            }
            return [];
        }
    }

    public static void Save<T>(string fileName, IEnumerable<T> data)
    {
        var path = PathOf(fileName);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOpts), Encoding.UTF8);
        File.Move(tmp, path, true);
    }

    /// <summary>深拷贝（基于 JSON 序列化），用于模板套用时隔离引用。</summary>
    public static T Clone<T>(T source)
    {
        var json = JsonSerializer.Serialize(source, JsonOpts);
        return JsonSerializer.Deserialize<T>(json, JsonOpts)!;
    }

    public static string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, JsonOpts);
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOpts);

    // ---------- 本地凭据混淆存储 ----------
    // 说明：使用本机用户维度派生的 AES 密钥加密，防止明文落盘。
    // 这不是强安全边界——同一用户会话内可解密，仅用于避免密码以明文形式出现在配置文件中。
    private static byte[] Key =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{Environment.UserName}@{Environment.MachineName}:NetConfigPro"));

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor(aes.Key, aes.IV);
        var body = enc.TransformFinalBlock(Encoding.UTF8.GetBytes(plain), 0, Encoding.UTF8.GetByteCount(plain));
        return Convert.ToBase64String(aes.IV.Concat(body).ToArray());
    }

    public static string Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return "";
        try
        {
            var raw = Convert.FromBase64String(cipher);
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = raw.Take(16).ToArray();
            using var dec = aes.CreateDecryptor(aes.Key, aes.IV);
            var body = dec.TransformFinalBlock(raw, 16, raw.Length - 16);
            return Encoding.UTF8.GetString(body);
        }
        catch
        {
            return "";
        }
    }
}
