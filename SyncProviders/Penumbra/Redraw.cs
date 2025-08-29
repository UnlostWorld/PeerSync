// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using PeerSync;

public static class PenumbraExtensions
{
	public static async Task RedrawAndWait(this PenumbraCommunicator penumbra, int objectTableIndex)
	{
		await penumbra.RedrawObject(objectTableIndex);

		bool isDrawing = false;
		Stopwatch sw = new();
		sw.Start();
		while (!isDrawing && sw.ElapsedMilliseconds < 10_000)
		{
			await Plugin.Framework.RunOnUpdate();

			unsafe
			{
				GameObject* pGameObject = GameObjectManager.Instance()->Objects.IndexSorted[objectTableIndex];
				if (pGameObject->RenderFlags != 0)
					break;

				if (pGameObject->ObjectKind == ObjectKind.Pc)
				{
					CharacterBase* pCharacter = (CharacterBase*)pGameObject->DrawObject;
					if (pCharacter == null)
						break;

					if (pCharacter->HasModelInSlotLoaded != 0)
						break;

					if (pCharacter->HasModelFilesInSlotLoaded != 0)
						break;
				}

				isDrawing = true;
			}
		}

		if (sw.ElapsedMilliseconds > 10_000)
			throw new Exception("Timed out waiting for character redraw");

		sw.Stop();
	}
}