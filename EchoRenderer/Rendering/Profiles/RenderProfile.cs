﻿using System;
using CodeHelpers;
using CodeHelpers.Mathematics;
using EchoRenderer.Mathematics;
using EchoRenderer.Rendering.Pixels;
using EchoRenderer.Textures.DimensionTwo;

namespace EchoRenderer.Rendering.Profiles
{
	/// <summary>
	/// An immutable record that defines the renderer's settings/parameters.
	/// Immutability ensures that the profile never change when all threads are running.
	/// </summary>
	public abstract record RenderProfile : IProfile
	{
		/// <summary>
		/// The target scene to render.
		/// </summary>
		public PressedScene Scene { get; init; }

		/// <summary>
		/// The fundamental rendering method used for each pixel.
		/// </summary>
		public PixelWorker Method { get; init; }

		/// <summary>
		/// The destination <see cref="RenderBuffer"/> to render onto.
		/// </summary>
		public RenderBuffer RenderBuffer { get; init; }

		/// <summary>
		/// The maximum number of worker threads concurrently running.
		/// </summary>
		public int WorkerSize { get; init; } = Environment.ProcessorCount;

		/// <summary>
		/// The maximum number of bounce allowed for one sample.
		/// </summary>
		public int BounceLimit { get; init; } = 128;

		/// <summary>
		/// Epsilon lower bound value to determine when an energy is essentially zero.
		/// </summary>
		public Float3 EnergyEpsilon { get; init; } = Utilities.ToFloat3(Utilities.CreateLuminance(12E-3f));

		/// <summary>
		/// The minimum number of consecutive samples performed on each pixel.
		/// </summary>
		public abstract int BaseSample { get; }

		/// <summary>
		/// Returns whether <paramref name="energy"/> is considered as empty or zero
		/// based on the <see cref="EnergyEpsilon"/> of this <see cref="RenderProfile"/>.
		/// </summary>
		public bool IsZero(in Float3 energy) => energy <= EnergyEpsilon;

		public virtual void Validate()
		{
			if (Scene == null) throw ExceptionHelper.Invalid(nameof(Scene), InvalidType.isNull);
			if (Method == null) throw ExceptionHelper.Invalid(nameof(Method), InvalidType.isNull);
			if (RenderBuffer == null) throw ExceptionHelper.Invalid(nameof(RenderBuffer), InvalidType.isNull);

			if (WorkerSize <= 0) throw ExceptionHelper.Invalid(nameof(WorkerSize), WorkerSize, InvalidType.outOfBounds);
			if (BounceLimit < 0) throw ExceptionHelper.Invalid(nameof(BounceLimit), BounceLimit, InvalidType.outOfBounds);
			if (EnergyEpsilon.MinComponent < 0f) throw ExceptionHelper.Invalid(nameof(EnergyEpsilon), EnergyEpsilon, InvalidType.outOfBounds);
		}
	}
}