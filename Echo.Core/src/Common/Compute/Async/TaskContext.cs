using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Echo.Core.Common.Diagnostics;
using Echo.Core.Common.Threading;

namespace Echo.Core.Common.Compute.Async;

class TaskContext
{
	public TaskContext(Action<uint> taskAction, uint repeatCount, uint workerCount) : this(repeatCount, workerCount)
	{
		Ensure.IsNotNull(taskAction);
		this.taskAction = taskAction;
	}

	/// <summary>
	/// Create this <see cref="TaskContext"/> as a signal.
	/// Can be set immediately using <see cref="FinishOnce"/>.
	/// </summary>
	public TaskContext() : this(1) { }

	protected TaskContext(uint repeatCount, uint workerCount = 1)
	{
		Ensure.IsTrue(repeatCount > 0);
		Ensure.IsTrue(workerCount > 0);

		partitionCount = Math.Min(repeatCount, workerCount);
		partitionSize = repeatCount / partitionCount;
		bigPartitions = repeatCount - partitionSize * partitionCount;

		Ensure.IsTrue(bigPartitions < workerCount);
	}

	public readonly uint partitionCount;
	readonly uint partitionSize;
	readonly uint bigPartitions;

	protected Action<uint> taskAction;

	uint launchedCount;
	uint finishedCount;

	protected readonly Locker locker = new();
	Action continuationAction;
	Exception innerException;

	static readonly Action emptyAction = () => { };

	public bool IsFinished => InterlockedHelper.Read(ref finishedCount) == partitionCount;

	public void Register(Action continuation)
	{
		using var _ = locker.Fetch();
		Ensure.IsNull(continuationAction);

		if (IsFinished || innerException != null)
		{
			continuation();
			continuationAction = emptyAction; //Ensure no double registration
		}
		else continuationAction = continuation;
	}

	public void SetException(Exception exception)
	{
		using var _ = locker.Fetch();
		if (innerException != null) return; //Only capture the first exception for now

		innerException = exception;
		continuationAction?.Invoke();
	}

	public void ThrowIfExceptionOccured()
	{
		using var _ = locker.Fetch();
		if (innerException == null) return;
		ExceptionDispatchInfo.Throw(innerException);
	}

	public void Execute(ref Procedure procedure)
	{
		uint partition = Interlocked.Increment(ref launchedCount) - 1;

		Ensure.IsTrue(partition < partitionCount);
		Ensure.IsNotNull(taskAction); //Ensure not created as a signal

		uint start = partitionSize * partition;
		uint end = start + partitionSize;
		if (partition < bigPartitions) ++end;

		procedure.Begin(end - start);

		for (uint i = start; i < end; i++)
		{
			taskAction(i);
			procedure.Advance();
		}

		FinishOnce();
	}

	public void FinishOnce()
	{
		uint finished = Interlocked.Increment(ref finishedCount) - 1;
		Ensure.IsTrue(finished < partitionCount);
		if (finished + 1 < partitionCount) return;
		lock (locker) continuationAction?.Invoke();
	}
}

class TaskContext<T> : TaskContext
{
	public TaskContext(Func<T> taskAction) : base(1)
	{
		Ensure.IsNotNull(taskAction);

		this.taskAction = _ =>
		{
			T item = taskAction();
			lock (locker) result = item;
		};
	}

	/// <summary>
	/// Create this <see cref="TaskContext{T}"/> as a signal.
	/// Can be set immediately using <see cref="FinishOnce"/>.
	/// </summary>
	public TaskContext() { }

	T result;

	public T GetResult()
	{
		Ensure.IsTrue(IsFinished);
		lock (locker) return result;
	}

	public void FinishOnce(T item)
	{
		Ensure.IsFalse(IsFinished);
		lock (locker) result = item;
		FinishOnce();
	}
}