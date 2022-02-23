﻿using System;
using System.Runtime.CompilerServices;

namespace EchoRenderer.Common.Memory;

public readonly struct View<T>
{
	public View(T[] array)
	{
		this.array = array;
		start = 0;
		Count = array.Length;
	}

	public View(T[] array, int start, int count)
	{
		this.array = array;
		this.start = start;
		Count = count;
	}

	public void Clear() => throw new NotImplementedException();

	public View<T> Slice(int offset) => throw new NotImplementedException();
	public View<T> Slice(int offset, int length) => throw new NotImplementedException();

	public static implicit operator ReadOnlyView<T>(View<T> view) =>
		new ReadOnlyView<T>(view.array, view.start, view.Count);

	public static implicit operator Span<T>(View<T> view) =>
		new Span<T>(view.array, view.start, view.Count);
	public static implicit operator ReadOnlySpan<T>(View<T> view) =>
		new ReadOnlySpan<T>(view.array, view.start, view.Count);

	public T this[int index]
	{
		get => array[IndexShift(index)];
		set => array[IndexShift(index)] = value;
	}

	public T this[Index index]
	{
		get => array[IndexShift(index.Value)];
		set => array[IndexShift(index.Value)] = value;
	}

	public View<T> this[Range range]
	{
		get => throw new NotImplementedException();
		set => throw new NotImplementedException();
	}

	public bool IsEmpty => Count == 0 || array == null;

	public int Count { get; }

	readonly T[] array;
	readonly int start;

	/// <summary>
	///     Shifts the view array index to the original array index
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	int IndexShift(int index) => index + start;
}
