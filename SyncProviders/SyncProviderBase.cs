// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Threading.Tasks;
using PeerSync;

public abstract class SyncProviderBase : IDisposable
{
	public bool IsDisposed { get; private set; }

	public abstract string Key { get; }

	public abstract Task<string?> Serialize(ushort objectIndex);
	public abstract Task Deserialize(string? lastContent, string? content, CharacterSync character);
	public virtual void DrawStatus() { }

	public virtual void Dispose()
	{
		this.IsDisposed = true;
	}

	public virtual void OnCharacterConnected(CharacterSync character)
	{
	}

	public virtual void OnCharacterDisconnected(CharacterSync character)
	{
	}
}