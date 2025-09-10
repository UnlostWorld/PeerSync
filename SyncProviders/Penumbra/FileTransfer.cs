// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface;

namespace PeerSync.SyncProviders.Penumbra;

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

		Task.Run(this.TransferSafe, this.cancellationToken);
	}

	public abstract float Progress { get; }
	public abstract FontAwesomeIcon Icon { get; }
	public string Name { get; protected set; } = string.Empty;
	public bool IsWaiting { get; private set; }
	public bool IsComplete { get; private set; }

	protected abstract Task Transfer();

	protected void Retry()
	{
		this.needsRetry = true;
	}

	private async Task TransferSafe()
	{
		try
		{
			do
			{
				this.needsRetry = false;
				this.IsComplete = false;
				this.sync.transfers.Add(this);

				this.IsWaiting = true;
				while (this.IsWaiting
					&& !this.cancellationToken.IsCancellationRequested)
				{
					lock (PenumbraSync.QueueLock)
					{
						this.IsWaiting = PenumbraSync.ActiveTransfers >= Configuration.Current.MaxTransfers;
					}

					await Task.Delay(250, this.cancellationToken);
				}

				this.IsWaiting = false;

				lock (PenumbraSync.QueueLock)
				{
					PenumbraSync.ActiveTransfers++;
				}

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
			if (!this.sync.transfers.TryRemove(this))
			{
				Plugin.Log.Error("Error removing transfer from queue");
			}

			lock (PenumbraSync.QueueLock)
			{
				PenumbraSync.ActiveTransfers--;
			}

			this.Dispose();
			GC.Collect();

			this.IsComplete = true;
		}
	}

	public virtual void Dispose()
	{
	}
}
