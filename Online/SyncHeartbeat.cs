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

public class SyncHeartbeat
{
	public string? Identifier { get; set; }
	public ushort Port { get; set; }
	public string? LocalAddress { get; set; }

	public Task Send(string indexServer)
	{
		string json = JsonSerializer.Serialize(this);
		return ServerApi.PostAsync($"{indexServer}/Heartbeat", json, "application/json");
	}
}
