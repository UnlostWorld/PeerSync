// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeerSync.SyncProviders.Penumbra;

public class PenumbraCommunicator : PluginCommunicatorBase
{
	protected override string InternalName => "Penumbra";
	protected override Version Version => new Version(1, 2, 0, 22);

	public enum PenumbraApiEc
	{
		Success = 0,
		NothingChanged = 1,
		CollectionMissing = 2,
		ModMissing = 3,
		OptionGroupMissing = 4,
		OptionMissing = 5,

		CharacterCollectionExists = 6,
		LowerPriority = 7,
		InvalidGamePath = 8,
		FileMissing = 9,
		InvalidManipulation = 10,
		InvalidArgument = 11,
		PathRenameFailed = 12,
		CollectionExists = 13,
		AssignmentCreationDisallowed = 14,
		AssignmentDeletionDisallowed = 15,
		InvalidIdentifier = 16,
		SystemDisposed = 17,
		AssignmentDeletionFailed = 18,

		TemporarySettingDisallowed = 19,
		TemporarySettingImpossible = 20,

		InvalidCredentials = 21,

		UnknownError = 255,
	}

	public enum RedrawType
	{
		Redraw,
		AfterGPose,
	}

	public async Task<Dictionary<string, HashSet<string>>?> GetGameObjectResourcePaths(ushort objectIndex)
	{
		if (!this.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		Dictionary<string, HashSet<string>>?[]? objectsResourcePaths
			= this.InvokeFunc<Dictionary<string, HashSet<string>>?[], ushort[]>(
				"Penumbra.GetGameObjectResourcePaths.V5",
				[objectIndex]);

		if (objectsResourcePaths == null || objectsResourcePaths.Length <= 0)
			return null;

		return objectsResourcePaths[0];
	}

	public async Task<string?> GetMetaManipulations(ushort objectIndex)
	{
		if (!this.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();
		return this.InvokeFunc<string, int>("Penumbra.GetMetaManipulations.V5", objectIndex);
	}

	public async Task RedrawObject(int objectIndex, RedrawType setting = RedrawType.Redraw)
	{
		if (!this.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		this.InvokeAction<int, int>(
			$"Penumbra.RedrawObject.V5",
			objectIndex,
			(int)setting);
	}

	// Temporary
	public async Task<Guid?> CreateTemporaryCollection(string identity, string name)
	{
		if (!this.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		(PenumbraApiEc returnCode, Guid guid) = this.InvokeFunc<(PenumbraApiEc, Guid), string, string>(
			$"Penumbra.CreateTemporaryCollection.V6",
			identity,
			name);

		if (returnCode != PenumbraApiEc.Success)
			throw new Exception($"Penumbra error: {returnCode}");

		return guid;
	}

	public async Task AssignTemporaryCollection(
		Guid collectionId,
		int actorIndex,
		bool forceAssignment)
	{
		if (!this.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		PenumbraApiEc returnCode = this.InvokeFunc<PenumbraApiEc, Guid, int, bool>(
			$"Penumbra.AssignTemporaryCollection.V5",
			collectionId,
			actorIndex,
			forceAssignment);

		if (returnCode != PenumbraApiEc.Success)
		{
			throw new Exception($"Penumbra error: {returnCode}");
		}
	}

	public async Task DeleteTemporaryCollection(Guid collectionId)
	{
		if (!this.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		PenumbraApiEc returnCode = this.InvokeFunc<PenumbraApiEc, Guid>(
			$"Penumbra.DeleteTemporaryCollection.V5",
			collectionId);

		if (returnCode != PenumbraApiEc.Success)
		{
			throw new Exception($"Penumbra error: {returnCode}");
		}
	}

	public async Task AddTemporaryMod(
		string tag,
		Guid collectionId,
		Dictionary<string, string> paths,
		string manipulationString,
		int priority)
	{
		if (!this.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		PenumbraApiEc returnCode = this.InvokeFunc<PenumbraApiEc, string, Guid, Dictionary<string, string>, string, int>(
			$"Penumbra.AddTemporaryMod.V5",
			tag,
			collectionId,
			paths,
			manipulationString,
			priority);

		if (returnCode != PenumbraApiEc.Success)
		{
			throw new Exception($"Penumbra error: {returnCode}");
		}
	}

	public async Task RemoveTemporaryMod(string tag, Guid collectionId, int priority)
	{
		if (!this.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		PenumbraApiEc returnCode = this.InvokeFunc<PenumbraApiEc, string, Guid, int>(
			$"Penumbra.RemoveTemporaryMod.V5",
			tag,
			collectionId,
			priority);

		if (returnCode != PenumbraApiEc.Success
			&& returnCode != PenumbraApiEc.NothingChanged)
		{
			throw new Exception($"Penumbra error: {returnCode}");
		}
	}
}