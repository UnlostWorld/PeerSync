// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Online;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class ServerApi
{
	private static readonly HttpClient Client;

	static ServerApi()
	{
		SocketsHttpHandler handler = new();
		handler.AutomaticDecompression = DecompressionMethods.All;
		handler.ConnectCallback = OnConnect;

		Client = new(handler);
	}

	public static async Task<string> GetAsync(string uri)
	{
		using HttpResponseMessage response = await Client.GetAsync(uri);
		return await response.Content.ReadAsStringAsync();
	}

	public static async Task<string> PostAsync(string uri, string data, string contentType)
	{
		using HttpContent content = new StringContent(data, Encoding.UTF8, contentType);

		HttpRequestMessage requestMessage = new HttpRequestMessage()
		{
			Content = content,
			Method = HttpMethod.Post,
			RequestUri = new Uri(uri),
		};

		using HttpResponseMessage response = await Client.SendAsync(requestMessage);

		try
		{
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsStringAsync();
		}
		catch (Exception ex)
		{
			throw new Exception($"Error posting message to {uri}\n{data}", ex);
		}
	}

	private static async ValueTask<Stream> OnConnect(SocketsHttpConnectionContext context, CancellationToken token)
	{
		// Force IPv4
		IPHostEntry entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, token);
		Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
		socket.NoDelay = true;

		try
		{
			await socket.ConnectAsync(entry.AddressList, context.DnsEndPoint.Port, token);
			return new NetworkStream(socket, ownsSocket: true);
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}
}
