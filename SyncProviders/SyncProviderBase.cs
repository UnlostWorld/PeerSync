// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Threading.Tasks;
using PeerSync;

public abstract class SyncProviderBase
{
	public abstract string Key { get; }

	public virtual void OnInitialized() { }
	public abstract Task<string?> Serialize(ushort objectIndex);
	public abstract Task Deserialize(string? content, CharacterSync character);
}