using System.Runtime.CompilerServices;
using CodeHelpers.Mathematics;
using CodeHelpers.Packed;
using Echo.Core.Aggregation.Bounds;
using Echo.Core.Aggregation.Primitives;
using Echo.Core.Common.Mathematics;
using Echo.Core.Common.Mathematics.Primitives;
using Echo.Core.Evaluation.Sampling;
using Echo.Core.Textures.Colors;

namespace Echo.Core.Scenic.Lights;

/// <summary>
/// A singularity <see cref="LightEntity"/> that emits from only one point.
/// </summary>
public class PointLight : LightEntity, ILightSource<PreparedPointLight>
{
	public PreparedPointLight Extract() => new(Intensity, ContainedPosition);
}

/// <summary>
/// The prepared version of <see cref="PointLight"/>.
/// </summary>
public readonly struct PreparedPointLight : IPreparedLight
{
	public PreparedPointLight(in RGB128 intensity, in Float3 position)
	{
		this.intensity = intensity;
		this.position = position;
		Power = 4f * Scalars.Pi * intensity.Luminance;
	}

	readonly RGB128 intensity;
	readonly Float3 position;

	/// <inheritdoc/>
	public float Power { get; }

	/// <inheritdoc/>
	public BoxBound BoxBound => new(position, position);

	/// <inheritdoc/>
	public ConeBound ConeBound => ConeBound.CreateFullSphere();

	/// <inheritdoc/>
	[SkipLocalsInit]
	public Probable<RGB128> Sample(in GeometryPoint origin, Sample2D sample, out Float3 incident, out float travel)
	{
		Float3 offset = position - origin;
		float travel2 = offset.SquaredMagnitude;

		if (!FastMath.Positive(travel2))
		{
			Unsafe.SkipInit(out incident);
			Unsafe.SkipInit(out travel);
			return Probable<RGB128>.Impossible;
		}

		travel = FastMath.Sqrt0(travel2);

		float travelR = 1f / travel;
		incident = offset * travelR;

		return new Probable<RGB128>(intensity * travelR * travelR, 1f);
	}

	/// <inheritdoc/>
	public float ProbabilityDensity(in GeometryPoint origin, in Float3 incident) => 1f;
}