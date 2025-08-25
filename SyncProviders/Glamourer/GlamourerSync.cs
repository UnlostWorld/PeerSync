// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Threading.Tasks;

namespace PeerSync.SyncProviders.Glamourer;

public class GlamourerSync : SyncProviderBase
{
	private readonly GlamourerCommunicator glamourer = new();

	public override string Key => "Glamourer";

	public override async Task<string?> Serialize(ushort objectIndex)
	{
		if (!glamourer.GetIsAvailable())
			return null;

		return await this.glamourer.GetState(objectIndex);
	}

	public override async Task Deserialize(string content, ushort objectIndex)
	{
		if (!glamourer.GetIsAvailable())
			return;

		await glamourer.SetState(objectIndex, content);
	}
}