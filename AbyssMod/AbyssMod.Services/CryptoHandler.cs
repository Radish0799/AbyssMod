using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AbyssMod.Services;

public sealed class CryptoHandler : DelegatingHandler
{
	public CryptoHandler(HttpMessageHandler innerHandler)
		: base(innerHandler)
	{
	}

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
		if (response.Content != null)
		{
			response.Content = new ByteArrayContent(Decrypt(await response.Content.ReadAsByteArrayAsync(cancellationToken)));
		}
		return response;
	}

	private static byte[] Decrypt(byte[] data)
	{
		string text = Encoding.UTF8.GetString(data);
		if (!text.StartsWith(Config.TranslationCryptoTag.Value))
		{
			return data;
		}
		string text2 = text;
		int length = Config.TranslationCryptoTag.Value.Length;
		return Xor(Convert.FromBase64String(text2.Substring(length, text2.Length - length)), Encoding.UTF8.GetBytes(Config.TranslationCryptoKey.Value));
	}

	private static byte[] Xor(byte[] data, byte[] key)
	{
		byte[] array = new byte[data.Length];
		for (int i = 0; i < data.Length; i++)
		{
			array[i] = (byte)(data[i] ^ key[i % key.Length]);
		}
		return array;
	}
}
