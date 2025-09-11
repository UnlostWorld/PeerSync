// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System;
using System.Threading;
using System.Threading.Tasks;

public abstract class FileTransfer : IDisposable
{
	public readonly CharacterSync Character;

	protected readonly PenumbraSync sync;
	protected readonly CancellationToken cancellationToken;
	protected readonly string hash;

	private bool needsRetry = false;

	public FileTransfer(PenumbraSync sync, string hash, CharacterSync character, CancellationToken token)
	{
		this.sync = sync;
		this.hash = hash;
		this.cancellationToken = token;
		this.Character = character;
	}

	public abstract float Progress { get; }
	public string Name { get; protected set; } = string.Empty;

	public async Task TransferSafe()
	{
		try
		{
			do
			{
				this.needsRetry = false;

				await this.Transfer();

				if (this.needsRetry)
				{
					await Task.Delay(1000, this.cancellationToken);
				}
			}
			while (this.needsRetry && !this.cancellationToken.IsCancellationRequested);
		}
		catch (TaskCanceledException)
		{
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error in file transfer");
		}
		finally
		{
			this.Dispose();
		}
	}

	public virtual void Dispose()
	{
	}

	protected abstract Task Transfer();

	protected void Retry()
	{
		this.needsRetry = true;
	}
}
