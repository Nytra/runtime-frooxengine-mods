// This file is part of SsaoDisable and is licensed under the GNU GPL v3.0.
// See LICENSE.txt file for full text.
// Copyright © 2023 Michael Ripley

using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SsaoDisable
{
	public class SsaoDisable : NeosMod
	{
		public override string Name => "SsaoDisable";
		public override string Author => "runtime";
		public override string Version => "1.0.2";
		public override string Link => "https://github.com/zkxs/SsaoDisable";

		private static bool _first_trigger = false;

		public override void OnEngineInit()
		{
			// disable future SSAO by patching the setup method
			Harmony harmony = new Harmony("net.michaelripley.SsaoDisable");
			MethodInfo originalMethod = AccessTools.DeclaredMethod(typeof(CameraInitializer), nameof(CameraInitializer.SetPostProcessing), new Type[] { typeof(Camera), typeof(bool), typeof(bool), typeof(bool) });
			if (originalMethod == null)
			{
				Error("Could not find CameraInitializer.SetPostProcessing(Camera, bool, bool, bool)");
				return;
			}
			MethodInfo replacementMethod = AccessTools.DeclaredMethod(typeof(SsaoDisable), nameof(SetPostProcessingPostfix));
			harmony.Patch(originalMethod, postfix: new HarmonyMethod(replacementMethod));
			Msg("Hooks installed successfully");
		}

		private static void SetPostProcessingPostfix(Camera c, bool enabled, bool motionBlur, bool screenspaceReflections)
		{
			try
			{
				AmplifyOcclusionEffect ssao = c.GetComponent<AmplifyOcclusionEffect>();
				if (ssao != null)
				{
					ssao.enabled = false;

					if (!_first_trigger)
					{
						_first_trigger = true;
						Msg("Hook triggered! Everything worked!");
						DisableExistingSsao(); // I have no idea why calling this here works but calling it earlier hard crashes unity
					}
				}
			}
			catch (Exception e)
			{
				Warn($"failed to disable a new SSAO: {e}");
			}
		}

		private static void DisableExistingSsao()
		{
			// disable prexisting SSAO by searching for all matching Unity components
			Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
			int count = 0;
			foreach (Camera camera in cameras)
			{
				try
				{
					var ssao = camera.GetComponent<AmplifyOcclusionEffect>();
					if (ssao != null && ssao.enabled)
					{
						ssao.enabled = false;
						count += 1;
					}
				}
				catch (Exception e)
				{
					Warn($"failed to disable a prexisting SSAO: {e}");
				}
			}
			Msg($"disabled {count} prexisting SSAOs");
		}
	}
}
