﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CodeHelpers;
using CodeHelpers.Diagnostics;
using CodeHelpers.Files;
using CodeHelpers.Mathematics;
using CodeHelpers.ObjectPooling;
using ForceRenderer.IO;
using ForceRenderer.Mathematics;
using ForceRenderer.Objects;
using ForceRenderer.Objects.SceneObjects;
using ForceRenderer.Rendering.Materials;
using Object = ForceRenderer.Objects.Object;

namespace ForceRenderer.Rendering
{
	/// <summary>
	/// A flattened out/pressed down record version of a scene for fast iteration.
	/// </summary>
	public class PressedScene
	{
		public PressedScene(Scene source)
		{
			ExceptionHelper.InvalidIfNotMainThread();
			this.source = source;

			List<PressedTriangle> triangleList = CollectionPooler<PressedTriangle>.list.GetObject();
			List<PressedSphere> sphereList = CollectionPooler<PressedSphere>.list.GetObject();

			Dictionary<Material, int> materialObjects = CollectionPooler<Material, int>.dictionary.GetObject();
			Queue<Object> frontier = CollectionPooler<Object>.queue.GetObject();

			//Find all rendering-related objects
			frontier.Enqueue(source);
			double totalArea = 0d; //Total area of all triangles combined

			while (frontier.Count > 0)
			{
				Object target = frontier.Dequeue();

				switch (target)
				{
					case SceneObject sceneObject:
					{
						int triangleCount = triangleList.Count;

						triangleList.AddRange(sceneObject.ExtractTriangles(ConvertMaterial));
						sphereList.AddRange(sceneObject.ExtractSpheres(ConvertMaterial));

						for (int i = triangleCount; i < triangleList.Count; i++) totalArea += triangleList[i].Area;

						break;

						int ConvertMaterial(Material material) //Converts a material into its material token
						{
							if (!materialObjects.TryGetValue(material, out int materialToken))
							{
								materialToken = materialObjects.Count;
								materialObjects.Add(material, materialToken);
							}

							return materialToken;
						}
					}
					case Camera value:
					{
						if (camera == null) camera = value;
						else DebugHelper.Log($"Multiple {nameof(Camera)} found! Only the first one will be used.");

						break;
					}
					case DirectionalLight value:
					{
						if (directionalLight.direction == default) directionalLight = new PressedDirectionalLight(value);
						else DebugHelper.Log($"Multiple {nameof(DirectionalLight)} found! Only the first one will be used.");

						break;
					}
				}

				Object.Children children = target.children;
				for (int i = 0; i < children.Count; i++) frontier.Enqueue(children[i]);
			}

			//Divide large triangles for better BVH space partitioning
			const float DivideThresholdMultiplier = 4.8f;
			const int DivideMaxIteration = 3;

			float fragmentThreshold = (float)(totalArea / triangleList.Count * DivideThresholdMultiplier);

			for (int i = 0; i < triangleList.Count; i++)
			{
				float multiplier = triangleList[i].Area / fragmentThreshold;
				if (multiplier > 0f) Fragment(MathF.Log2(multiplier).Ceil());

				void Fragment(int iteration) //Should be placed in a local method because of stack allocation
				{
					iteration = iteration.Clamp(0, DivideMaxIteration);

					int subdivision = 1 << (iteration * 2);
					Span<PressedTriangle> divided = stackalloc PressedTriangle[subdivision];

					triangleList[i].GetSubdivided(divided, iteration);
					triangleList[i] = divided[0];

					for (int j = 1; j < subdivision; j++) triangleList.Add(divided[j]);
				}
			}

			//Extract pressed data and construct BVH acceleration structure
			materials = materialObjects.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToArray();
			for (int i = 0; i < materials.Length; i++) materials[i].Press();

			triangles = new PressedTriangle[triangleList.Count];
			spheres = new PressedSphere[sphereList.Count];

			var aabbs = CollectionPooler<AxisAlignedBoundingBox>.list.GetObject();
			var indices = CollectionPooler<int>.list.GetObject();

			aabbs.Capacity = indices.Capacity = triangles.Length + spheres.Length;

			for (int i = 0; i < triangles.Length; i++)
			{
				var triangle = triangles[i] = triangleList[i];

				aabbs.Add(triangle.AABB);
				indices.Add(i);
			}

			for (int i = 0; i < spheres.Length; i++)
			{
				var sphere = spheres[i] = sphereList[i];

				aabbs.Add(sphere.AABB);
				indices.Add(~i);
			}

			bvh = new BoundingVolumeHierarchy(this, aabbs, indices);

			//Release resources
			CollectionPooler<PressedTriangle>.list.ReleaseObject(triangleList);
			CollectionPooler<PressedSphere>.list.ReleaseObject(sphereList);

			CollectionPooler<Material, int>.dictionary.ReleaseObject(materialObjects);
			CollectionPooler<Object>.queue.ReleaseObject(frontier);

			CollectionPooler<AxisAlignedBoundingBox>.list.ReleaseObject(aabbs);
			CollectionPooler<int>.list.ReleaseObject(indices);
		}

		public readonly Scene source;
		public readonly BoundingVolumeHierarchy bvh;

		public readonly Camera camera;
		public readonly PressedDirectionalLight directionalLight;

		readonly PressedTriangle[] triangles;
		readonly PressedSphere[] spheres;
		readonly Material[] materials;

		public int TriangleCount => triangles.Length;
		public int SphereCount => spheres.Length;
		public int MaterialCount => materials.Length;

		/// <summary>
		/// Returns the intersection status with one object of <paramref name="token"/>.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float Intersect(in Ray ray, int token, out Float2 uv)
		{
			if (token < 0)
			{
				ref PressedSphere sphere = ref spheres[~token];
				return sphere.GetIntersection(ray, out uv);
			}

			ref PressedTriangle triangle = ref triangles[token];
			return triangle.GetIntersection(ray, out uv);
		}

		/// <summary>
		/// Gets the normal of intersection with <paramref name="hit"/>.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Float3 GetNormal(in Hit hit)
		{
			if (hit.token < 0)
			{
				ref PressedSphere sphere = ref spheres[~hit.token];
				return sphere.GetNormal(hit.uv);
			}

			ref PressedTriangle triangle = ref triangles[hit.token];
			return triangle.GetNormal(hit.uv);
		}

		/// <summary>
		/// Gets the texcoord of intersection with <paramref name="hit"/>.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Float2 GetTexcoord(in Hit hit)
		{
			if (hit.token < 0) return hit.uv; //Sphere directly uses the uv as texcoord

			ref PressedTriangle triangle = ref triangles[hit.token];
			return triangle.GetTexcoord(hit.uv);
		}

		/// <summary>
		/// Gets the material of intersection with <paramref name="hit"/>.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Material GetMaterial(in Hit hit)
		{
			int materialToken;

			if (hit.token < 0)
			{
				ref PressedSphere sphere = ref spheres[~hit.token];
				materialToken = sphere.materialToken;
			}
			else
			{
				ref PressedTriangle triangle = ref triangles[hit.token];
				materialToken = triangle.materialToken;
			}

			return materials[materialToken];
		}

		public void Write(FileWriter writer)
		{
			//TODO: Not writing camera, directional light, and skybox right now
		}
	}
}