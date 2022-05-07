﻿using System;
using CodeHelpers.Mathematics;
using CodeHelpers.Packed;
using Echo.Common.Mathematics;
using Echo.Common.Mathematics.Primitives;
using Echo.Common.Mathematics.Randomization;
using Echo.Core.Evaluation.Distributions;
using Echo.Core.Textures;

namespace Echo.Core.Scenic;

public class Camera : Entity
{
	public Camera(float fieldOfView) => FieldOfView = fieldOfView;

	float fieldOfView;
	float fieldDistance;

	/// <summary>
	/// Horizontal field of view in degrees.
	/// </summary>
	public float FieldOfView
	{
		get => fieldOfView;
		set
		{
			fieldOfView = value;
			fieldDistance = 0.5f / MathF.Tan(Scalars.ToRadians(value) / 2f);
		}
	}

	/// <summary>
	/// The distance at which the image should be fully sharp.
	/// NOTE: This only affects depth of field.
	/// </summary>
	public float FocalLength { get; set; } = 12f;

	/// <summary>
	/// The intensity of the depth of field blur.
	/// NOTE: This only affects depth of field.
	/// </summary>
	public float Aperture { get; set; } = 0f;

	/// <summary>
	/// Returns a ray emitted from the camera at <paramref name="uv"/>.
	/// </summary>
	/// <param name="uv">X component from -0.5 to 0.5; Y component an aspect radio corrected version of X.</param>
	/// <param name="random">An PRNG used for Depth of Field. Can be null if no DoF is wanted.</param>
	public Ray GetRay(Float2 uv, Prng random = null)
	{
		Float3 direction = uv.CreateXY(fieldDistance);

		if (FastMath.AlmostZero(Aperture) || random == null)
		{
			//No depth of field

			direction = LocalToWorld.MultiplyDirection(direction);
			return new Ray(Position, direction.Normalized);
		}

		//With randomized origin to add depth of field

		Float3 origin = random.Next2(-Aperture, Aperture).XY_;
		direction = direction.Normalized * FocalLength - origin;

		origin = LocalToWorld.MultiplyPoint(origin);
		direction = LocalToWorld.MultiplyDirection(direction);

		return new Ray(origin, direction.Normalized);
	}

	public Ray SpawnRay(CameraSample sample, TextureRegion region)
	{

	}

	public void LookAt(Entity target) => LookAt(target.Position);

	public void LookAt(Float3 target)
	{
		Float3 to = (target - Position).Normalized;

		float yAngle = -Float2.Up.SignedAngle(to.XZ);
		float xAngle = -Float2.Right.SignedAngle(to.RotateXZ(yAngle).ZY);

		Rotation = new Float3(xAngle, yAngle, 0f);
	}
}