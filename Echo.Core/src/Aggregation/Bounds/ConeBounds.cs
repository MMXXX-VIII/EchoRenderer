﻿using System;
using CodeHelpers.Diagnostics;
using CodeHelpers.Mathematics;
using CodeHelpers.Packed;
using Echo.Core.Common.Mathematics;

namespace Echo.Core.Aggregation.Bounds;

public readonly struct ConeBounds
{
	public ConeBounds(in Float3 axis, float cosOffset, float cosExtend)
	{
		Assert.AreEqual(axis.SquaredMagnitude, 1f);
		Assert.IsTrue(cosOffset is >= -1f and <= 1f);
		Assert.IsTrue(cosExtend is >= 0f and <= 1f);

		this.axis = axis;
		this.cosOffset = cosOffset;
		this.cosExtend = cosExtend;
	}

	public readonly Float3 axis;
	public readonly float cosOffset;
	public readonly float cosExtend;

	public float Area //relative
	{
		get
		{
			float offset = MathF.Acos(FastMath.Clamp11(cosOffset));
			float extend = MathF.Acos(FastMath.Clamp11(cosExtend));

			float angle = FastMath.Min(offset + extend, Scalars.Pi) * 2f;
			float sinOffset = FastMath.Identity(cosOffset);

			return Scalars.Tau * (1f - cosOffset) + Scalars.Pi / 2f *
			(
				angle * sinOffset - MathF.Cos(offset - angle) -
				2f * offset * sinOffset + cosOffset
			);
		}
	}

	public ConeBounds Encapsulate(in ConeBounds other) => other.cosOffset > cosOffset ? Union(this, other) : Union(other, this);

	static ConeBounds Union(in ConeBounds value0, in ConeBounds value1)
	{
		float offset0 = MathF.Acos(FastMath.Clamp11(value0.cosOffset));
		float offset1 = MathF.Acos(FastMath.Clamp11(value1.cosOffset));

		Assert.IsTrue(offset0 >= offset1);

		Float3 axis = value0.axis;
		float offset = offset0;
		float max = value0.axis.Angle(value1.axis) + offset1;

		if (FastMath.Min(max, Scalars.Pi) > offset)
		{
			//Create new cone over the two inputs if the value0 is not large enough already
			offset = (offset + max) / 2f;

			if (offset < Scalars.Pi)
			{
				//Rotate axis to the middle of the two axes of the two inputs
				Float3 cross = axis.Cross(value1.axis);
				float rotation = offset - offset0;
				axis = new Versor(cross, rotation) * axis;
			}
			else offset = Scalars.Pi;
		}

		return new ConeBounds(axis, MathF.Cos(offset), FastMath.Min(value0.cosExtend, value1.cosExtend));
	}
}