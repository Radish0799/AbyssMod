using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace AbyssCdnRouter;

public static class CdnRouter
{
	public static async Task<HttpResponseMessage> GetDualAsync(HttpClient client, string url, bool preferRaw)
	{
		string expectedMd5 = TryGetExpectedMd5(url);
		string text = AddCacheBust(ToJsDelivr(url), expectedMd5);
		string text2 = AddCacheBust(ToRaw(url), expectedMd5);
		string first = (preferRaw ? text2 : text);
		string second = (preferRaw ? text : text2);
		HttpResponseMessage firstResp = null;
		try
		{
			firstResp = await client.GetAsync(first);
			HttpResponseMessage httpResponseMessage = await VerifyOrNullAsync(firstResp, expectedMd5);
			if (httpResponseMessage != null && (httpResponseMessage.IsSuccessStatusCode || string.Equals(first, second, StringComparison.Ordinal)))
			{
				return httpResponseMessage;
			}
		}
		catch when (!string.Equals(first, second, StringComparison.Ordinal))
		{
		}
		if (!string.Equals(first, second, StringComparison.Ordinal))
		{
			try
			{
				HttpResponseMessage httpResponseMessage2 = await VerifyOrNullAsync(await client.GetAsync(second), expectedMd5);
				if (httpResponseMessage2 != null)
				{
					return httpResponseMessage2;
				}
			}
			catch
			{
			}
		}
		if (firstResp != null && !firstResp.IsSuccessStatusCode)
		{
			return firstResp;
		}
		return new HttpResponseMessage(HttpStatusCode.BadGateway)
		{
			ReasonPhrase = "AbyssCdnRouter content MD5 verification failed"
		};
	}

	private static async Task<HttpResponseMessage> VerifyOrNullAsync(HttpResponseMessage response, string expectedMd5)
	{
		if (response == null || !response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(expectedMd5))
		{
			return response;
		}
		byte[] array = await response.Content.ReadAsByteArrayAsync();
		if (!string.Equals(Md5Hex(array), expectedMd5, StringComparison.OrdinalIgnoreCase))
		{
			response.Dispose();
			return null;
		}
		response.Content = new ByteArrayContent(array);
		return response;
	}

	private static string TryGetExpectedMd5(string url)
	{
		try
		{
			if (!TryGetCategory(url, out var group, out var key, out var lang))
			{
				return null;
			}
			string path = Path.Combine(GetAbyssModDir(), "cache", "translations", "manifest", lang + ".json");
			if (!File.Exists(path))
			{
				return null;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			JsonElement rootElement = jsonDocument.RootElement;
			JsonElement value;
			if (group == null)
			{
				return rootElement.TryGetProperty(key, out value) ? value.GetString() : null;
			}
			JsonElement value2;
			JsonElement value3;
			return (rootElement.TryGetProperty(group, out value2) && value2.ValueKind == JsonValueKind.Object && value2.TryGetProperty(key, out value3)) ? value3.GetString() : null;
		}
		catch
		{
			return null;
		}
	}

	private static bool TryGetCategory(string url, out string group, out string key, out string lang)
	{
		group = null;
		key = null;
		lang = null;
		string[] array = new Uri(url, UriKind.Absolute).AbsolutePath.Trim('/').Split('/');
		if (array.Length < 2 || !array[^1].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		lang = Path.GetFileNameWithoutExtension(array[^1]);
		string text = array[^2];
		if (string.Equals(text, "manifest", StringComparison.Ordinal))
		{
			return false;
		}
		if (array.Length >= 3 && (string.Equals(array[^3], "add-on", StringComparison.Ordinal) || string.Equals(array[^3], "other", StringComparison.Ordinal)))
		{
			group = ((array[^3] == "add-on") ? "add_on" : array[^3]);
			key = text;
			return true;
		}
		key = text;
		if (!string.Equals(key, "names", StringComparison.Ordinal) && !string.Equals(key, "ui_texts", StringComparison.Ordinal))
		{
			return string.Equals(key, "static", StringComparison.Ordinal);
		}
		return true;
	}

	private static string GetAbyssModDir()
	{
		string directoryName = Path.GetDirectoryName(typeof(CdnRouter).Assembly.Location);
		if (!string.IsNullOrEmpty(directoryName))
		{
			return directoryName;
		}
		return AppContext.BaseDirectory;
	}

	private static string AddCacheBust(string url, string expectedMd5)
	{
		if (string.IsNullOrWhiteSpace(expectedMd5) || url.Contains("?v=", StringComparison.Ordinal) || url.Contains("&v=", StringComparison.Ordinal))
		{
			return url;
		}
		return url + (url.Contains("?", StringComparison.Ordinal) ? "&" : "?") + "v=" + Uri.EscapeDataString(expectedMd5);
	}

	private static string Md5Hex(byte[] bytes)
	{
		return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
	}

	private static string ToJsDelivr(string url)
	{
		int num = url.IndexOf("raw.githubusercontent.com/", StringComparison.Ordinal);
		if (num < 0)
		{
			return url;
		}
		return "https://cdn.jsdelivr.net/gh/" + url.Substring(num + "raw.githubusercontent.com/".Length).Replace("/refs/heads/", "@");
	}

	private static string ToRaw(string url)
	{
		int num = url.IndexOf("cdn.jsdelivr.net/gh/", StringComparison.Ordinal);
		if (num < 0)
		{
			return url;
		}
		return "https://raw.githubusercontent.com/" + url.Substring(num + "cdn.jsdelivr.net/gh/".Length).Replace("@", "/refs/heads/");
	}
}
