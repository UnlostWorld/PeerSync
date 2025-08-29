// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System.Threading.Tasks;

public static class PenumbraExtensions
{
	public static async Task RedrawAndWait(this PenumbraCommunicator penumbra, int objectTableIndex)
	{
		await penumbra.RedrawObject(objectTableIndex);
		await Task.Delay(1000);
	}
}