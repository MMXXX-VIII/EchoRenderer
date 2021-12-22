﻿using System;
using System.Collections.Generic;
using CodeHelpers.Mathematics;
using EchoRenderer.Objects.GeometryObjects;
using EchoRenderer.Rendering.Materials;
using EchoRenderer.Textures;
using EchoRenderer.Textures.Directional;

namespace EchoRenderer.Objects.Scenes
{
	public class Scene : ObjectPack
	{
		public readonly List<DirectionalTexture> skyboxes = new();

		public DirectionalTexture Skybox
		{
			get => skyboxes.Count > 0 ? skyboxes[0] : null;
			set
			{
				if (skyboxes.Count > 0) skyboxes[0] = value;
				else skyboxes.Add(value);
			}
		}
	}

	public class StandardScene : Scene
	{
		public StandardScene(Material ground = null)
		{
			Skybox = new Cubemap("Assets/Cubemaps/OutsideSea");

			children.Add(new PlaneObject(ground ?? new Matte {Albedo = new Pure(0.75f)}, new Float2(32f, 24f)));
			// children.Add(new Light {Intensity = Utilities.ToColor("#c9e2ff").XYZ, Rotation = new Float3(60f, 60f, 0f)});

			children.Add(new Camera(110f) {Position = new Float3(0f, 3f, -6f), Rotation = new Float3(30f, 0f, 0f)});
		}
	}
}