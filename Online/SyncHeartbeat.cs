// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

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
