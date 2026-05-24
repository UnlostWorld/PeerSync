// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System;
using System.Threading;
using System.Threading.Tasks;
using PeerSync.Connections;

public abstract class FileTransfer : IDisposable
{
	public readonly CharacterConnection Character;

	protected readonly PenumbraSync sync;
	protected readonly CancellationToken cancellationToken;
	protected readonly string hash;

	private readonly CancellationTokenSource transferTaskTokenSource = new();
	private bool needsRetry = false;

	public FileTransfer(PenumbraSync sync, string hash, CharacterConnection character, byte queueIndex = 255)
	{
		this.sync = sync;
		this.hash = hash;
		this.cancellationToken = this.transferTaskTokenSource.Token;
		this.Character = character;
		this.ClientQueueIndex = queueIndex;
	}

	public abstract long Total { get; }
	public abstract long Current { get; }
	public byte ClientQueueIndex { get; protected set; }

	public float Progress => (float)this.Current / (float)this.Total;
	public string Name { get; protected set; } = string.Empty;

	public bool IsCanceled => this.transferTaskTokenSource.IsCancellationRequested;

	public void Cancel()
	{
		this.transferTaskTokenSource.Cancel();
	}

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
