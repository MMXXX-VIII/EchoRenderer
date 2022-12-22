﻿using Echo.Core.Common.Diagnostics;
using Echo.Core.Common.Mathematics;
using Echo.Core.Common.Mathematics.Primitives;
using Echo.Core.Common.Packed;
using Echo.Core.Evaluation.Sampling;
using Echo.Core.Textures.Colors;

namespace Echo.Core.Evaluation.Scattering;

public sealed class GlossyReflection<TMicrofacet, TFresnel> : BxDF where TMicrofacet : IMicrofacet
																   where TFresnel : IFresnel
{
	public GlossyReflection() : base(FunctionType.Glossy | FunctionType.Reflective) { }

	TMicrofacet microfacet;
	TFresnel fresnel;

	public void Reset(in TMicrofacet newMicrofacet, in TFresnel newFresnel)
	{
		microfacet = newMicrofacet;
		fresnel = newFresnel;
	}

	public override RGB128 Evaluate(in Float3 outgoing, in Float3 incident)
	{
		if (FlatOrOppositeHemisphere(outgoing, incident)) return RGB128.Black;
		if (!GlossyFresnel<TMicrofacet>.FindNormal(outgoing, incident, out Float3 normal)) return RGB128.Black;

		float cosO = CosineP(outgoing);
		float cosI = CosineP(incident);

		RGB128 evaluated = fresnel.Evaluate(FastMath.Abs(outgoing.Dot(normal))) / (4f * cosO * cosI);
		float ratio = microfacet.ProjectedArea(normal) * microfacet.Visibility(outgoing, incident);
		return evaluated * ratio;
	}

	public override float ProbabilityDensity(in Float3 outgoing, in Float3 incident)
	{
		if (FlatOrOppositeHemisphere(outgoing, incident)) return 0f;
		if (!GlossyFresnel<TMicrofacet>.FindNormal(outgoing, incident, out Float3 normal)) return 0f;
		return microfacet.ProbabilityDensity(outgoing, normal) / FastMath.Abs(outgoing.Dot(normal) * 4f);
	}

	public override Probable<RGB128> Sample(Sample2D sample, in Float3 outgoing, out Float3 incident)
	{
		Float3 normal = microfacet.Sample(outgoing, sample);
		incident = Float3.Reflect(outgoing, normal);

		Ensure.AreEqual(normal.SquaredMagnitude, 1f);
		Ensure.AreEqual(incident.SquaredMagnitude, 1f);

		if (FlatOrOppositeHemisphere(outgoing, incident)) return Probable<RGB128>.Impossible;

		float cosO = CosineP(outgoing);
		float cosI = CosineP(incident);

		RGB128 evaluated = fresnel.Evaluate(FastMath.Abs(outgoing.Dot(normal))) / (4f * cosO * cosI);
		float ratio = microfacet.ProjectedArea(normal) * microfacet.Visibility(outgoing, incident);
		float pdf = microfacet.ProbabilityDensity(outgoing, normal) / FastMath.Abs(outgoing.Dot(normal) * 4f);

		return (evaluated * ratio, pdf);
	}
}

public sealed class GlossyTransmission<TMicrofacet> : BxDF where TMicrofacet : IMicrofacet
{
	public GlossyTransmission() : base(FunctionType.Glossy | FunctionType.Transmissive) { }

	TMicrofacet microfacet;
	RealFresnel fresnel;

	public void Reset(in TMicrofacet newMicrofacet, RealFresnel newFresnel)
	{
		microfacet = newMicrofacet;
		fresnel = newFresnel;
	}

	public override RGB128 Evaluate(in Float3 outgoing, in Float3 incident)
	{
		if (FlatOrSameHemisphere(outgoing, incident)) return RGB128.Black;

		var packet = fresnel.CreateIncomplete(CosineP(outgoing));
		float etaR = packet.etaIncident / packet.etaOutgoing;
		
		if (!GlossyFresnel<TMicrofacet>.FindNormal(outgoing, incident * etaR, out Float3 normal)) return RGB128.Black;

		float dotO = outgoing.Dot(normal);
		float dotI = incident.Dot(normal);

		if (FastMath.Positive(dotO * dotI)) return RGB128.Black;
		RGB128 evaluated = RGB128.White - fresnel.Evaluate(dotO);
		if (evaluated.IsZero) return RGB128.Black;

		float numerator = etaR * etaR * dotO * dotI;
		float denominator = FastMath.FMA(etaR, dotI, dotO);
		float ratio = microfacet.ProjectedArea(normal) * microfacet.Visibility(outgoing, incident);

		denominator *= denominator * CosineP(outgoing) * CosineP(incident);
		return evaluated * ratio * FastMath.Abs(numerator / denominator);
	}

	public override float ProbabilityDensity(in Float3 outgoing, in Float3 incident)
	{
		if (FlatOrSameHemisphere(outgoing, incident)) return 0f;

		var packet = fresnel.CreateIncomplete(CosineP(outgoing));
		float etaR = packet.etaIncident / packet.etaOutgoing;

		if (!GlossyFresnel<TMicrofacet>.FindNormal(outgoing, incident * etaR, out Float3 normal)) return 0f;

		float dotO = outgoing.Dot(normal);
		float dotI = incident.Dot(normal);

		if (FastMath.Positive(dotO * dotI)) return 0f;
		float numerator = FastMath.Abs(etaR * etaR * dotI);
		float denominator = FastMath.FMA(etaR, dotI, dotO);
		denominator *= denominator;

		return microfacet.ProbabilityDensity(outgoing, normal) * numerator / denominator;
	}

	public override Probable<RGB128> Sample(Sample2D sample, in Float3 outgoing, out Float3 incident)
	{
		Float3 normal = microfacet.Sample(outgoing, sample);
		float dotO = outgoing.Dot(normal);
		Ensure.IsTrue(normal.Z >= 0f);

		var packet = fresnel.CreateIncomplete(dotO).Complete;

		if (packet.TotalInternalReflection)
		{
			incident = default;
			return Probable<RGB128>.Impossible;
		}

		incident = packet.Refract(outgoing, normal);
		float dotI = incident.Dot(normal);

		if (FlatOrSameHemisphere(outgoing, incident) || FastMath.Positive(dotO * dotI)) return Probable<RGB128>.Impossible;

		float etaR = packet.etaIncident / packet.etaOutgoing;
		float numerator = FastMath.Abs(etaR * etaR * dotI);
		float denominator = FastMath.FMA(etaR, dotI, dotO);
		denominator *= denominator;

		float evaluated = numerator * dotO / (denominator * CosineP(outgoing) * CosineP(incident));
		float ratio = microfacet.ProjectedArea(normal) * microfacet.Visibility(outgoing, incident);
		float pdf = microfacet.ProbabilityDensity(outgoing, normal) * numerator / denominator;

		return (new RGB128(1f - packet.Value) * FastMath.Abs(evaluated) * ratio, pdf);
	}
}

public sealed class GlossyFresnel<TMicrofacet> : BxDF where TMicrofacet : IMicrofacet
{
	public GlossyFresnel(FunctionType type) : base(type) { }
	
	public override RGB128 Evaluate(in Float3 outgoing, in Float3 incident) => throw new System.NotImplementedException();

	public override float ProbabilityDensity(in Float3 outgoing, in Float3 incident) => throw new System.NotImplementedException();
	
	public override Probable<RGB128> Sample(Sample2D sample, in Float3 outgoing, out Float3 incident) => throw new System.NotImplementedException();
	
	public static bool FindNormal(in Float3 outgoing, in Float3 incident, out Float3 normal)
	{
		normal = outgoing + incident;
		float length2 = normal.SquaredMagnitude;

		if (!FastMath.Positive(length2)) return false;
		normal *= FastMath.SqrtR0(length2); //Normalize
		if (normal.Z < 0f) normal = -normal;

		return true;
	}
}