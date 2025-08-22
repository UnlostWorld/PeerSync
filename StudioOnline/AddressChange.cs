// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioOnline.Sync;

using System.Threading.Tasks;
using System.Text.Json;

public class AddressChange
{
	public string? Address { get; set; }
	public string? Id { get; set; }
	public int? Port { get; set; }

	public Task<string> Send()
	{
		string json = JsonSerializer.Serialize(this);
		return ServerApi.PostAsync("Sync/Address", json, "application/json");
	}
}
