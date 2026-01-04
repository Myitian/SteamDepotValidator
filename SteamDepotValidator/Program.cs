using HtmlAgilityPack;
using System.Net;
using System.Security.Cryptography;

Console.WriteLine("""
    > [!IMPORTANT]
    >
    > It is recommend to use DepotDownloader to validate files if you have ownership to the depot on your account.
    > https://github.com/SteamRE/DepotDownloader
    >
    > This program should only be used when you lack access to the depot but still need to validate files.
    >
    > SteamDB does not allow scraping/crawling. Use this tool at your own risk.
    > https://steamdb.info/faq/#can-i-use-auto-refreshing-plugins-or-automatically-scrape-crawl-steamdb
    >
    """);
using SocketsHttpHandler handler = new()
{
    AllowAutoRedirect = true,
    AutomaticDecompression = DecompressionMethods.All,
    UseCookies = true,
    UseProxy = true
};
Console.WriteLine("""
    
    # Cookie
    """);
ReadOnlySpan<char> cookie = Console.ReadLine().AsSpan();
// A simple cookie parser
foreach (Range range in cookie.Split(';'))
{
    ReadOnlySpan<char> kvp = cookie[range];
    string name;
    string? value;
    int eq = kvp.IndexOf('=');
    if (eq < 0)
    {
        name = new(kvp.TrimStart(' '));
        value = null;
    }
    else
    {
        name = new(kvp[..eq].TrimStart(' '));
        value = new(kvp[(eq + 1)..]);
    }
    handler.CookieContainer.Add(new Cookie(name, value, "/", "steamdb.info"));
}
using HttpClient http = new(handler)
{
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
};
Console.WriteLine("""
    
    # User Agent
    """);
http.DefaultRequestHeaders.Add("User-Agent", Console.ReadLine());
http.DefaultRequestHeaders.Add("Accept", "text/html");
Console.WriteLine("""
    
    # Depot ID
    """);
string url = $"https://steamdb.info/depot/{Console.ReadLine()}/?show_hashes";
HtmlDocument doc = new();
using (Stream stream = await http.GetStreamAsync(url))
    doc.Load(stream);
Console.WriteLine("""
    
    # Content
    """);
foreach ((string path, (byte[] hashStart, byte[] hashEnd), long len) in doc.DocumentNode
    .Descendants("table")
    .First(it => it.HasClass("depot-files"))
    .Descendants("tbody")
    .First()
    .Descendants("tr")
    .Select(it => (
        // File/folder path
        HtmlEntity.DeEntitize(it.Descendants("td").First().InnerText),
        // Part of the file's SHA-1 hash (empty for folders)
        // SteamDB will hide the middle part of SHA-1 hash.
        ParseHashParts(it.Descendants("td").Skip(1).First().InnerText),
        // File length (-1 for folders)
        // The attribute should always existed even for folders, but HtmlAgilityPack requires a default value.
        long.Parse(it.Descendants("td").Skip(2).First().GetAttributeValue("data-sort", "-1")))))
{
    Console.Write("- ");
    Console.WriteLine(path);
    if (len == -1)
    {
        Console.Write("  Directory: ");
        if (!Directory.Exists(path))
            Console.WriteLine("MISSING");
        else
            Console.WriteLine("OK");
    }
    else
    {
        Console.Write("  File: ");
        FileInfo file = new(path);
        if (!file.Exists)
            Console.WriteLine("MISSING");
        else if (file.Length != len)
            Console.WriteLine("LENGTH ERROR");
        else
        {
            byte[] hash;
            using (FileStream fs = file.OpenRead())
                hash = SHA1.HashData(fs);
            if (!hash.StartsWith(hashStart) || !hash.EndsWith(hashEnd))
                Console.WriteLine("HASH ERROR");
            else
                Console.WriteLine("OK");
        }
    }
}

static (byte[], byte[]) ParseHashParts(ReadOnlySpan<char> text)
{
    int asteriskL = text.IndexOf('*');
    if (asteriskL < 0)
        return (Convert.FromHexString(text), []);
    int asteriskR = text.LastIndexOf('*') + 1;
    return (Convert.FromHexString(text[..asteriskL]), Convert.FromHexString(text[asteriskR..]));
}