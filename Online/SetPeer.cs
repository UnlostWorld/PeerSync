// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Online;

using System.Threading.Tasks;
using System.Text.Json;
using System;

public class SetPeer
{
	public string? Fingerprint { get; set; }
	public ushort Port { get; set; }
	public string? LocalAddress { get; set; }

	public async Task<ServerStatus> Send(string indexServer)
	{
		string json = JsonSerializer.Serialize(this);
		json = await ServerApi.PostAsync($"{indexServer.TrimEnd('/')}/Peer/Set", json, "application/json");
		JsonSerializerOptions options = new();
		options.PropertyNameCaseInsensitive = true;
		return JsonSerializer.Deserialize<ServerStatus>(json, options) ?? throw new Exception();
	}
}

public class ServerStatus
{
	public string? Motd { get; set; }
	public string? ServerName { get; set; }
	public int OnlineUsers { get; set; }
}
