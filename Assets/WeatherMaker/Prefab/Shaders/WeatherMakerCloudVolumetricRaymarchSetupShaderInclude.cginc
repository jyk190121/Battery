// Weather Maker for Unity
// (c) 2016 Digital Ruby, LLC
// Source code may be used for personal or commercial projects.
// Source code may NOT be redistributed or sold.
// 
// *** A NOTE ABOUT PIRACY ***
// 
// If you got this asset from a pirate site, please consider buying it from the Unity asset store at https://assetstore.unity.com/packages/slug/60955?aid=1011lGnL. This asset is only legally available from the Unity Asset Store.
// 
// I'm a single indie dev supporting my family by spending hundreds and thousands of hours on this and other assets. It's very offensive, rude and just plain evil to steal when I (and many others) put so much hard work into the software.
// 
// Thank you.
//
// *** END NOTE ABOUT PIRACY ***
//

#ifndef __WEATHER_MAKER_CLOUD_VOLUMETRIC_RAYMARCH_SETUP_SHADER__
#define __WEATHER_MAKER_CLOUD_VOLUMETRIC_RAYMARCH_SETUP_SHADER__

#include "WeatherMakerMathShaderInclude.cginc"

float3 ComputeCloudRaymarchDir(float3 rayDir)
{
	float cloudRayOffset = lerp(_CloudRayOffsetVolumetric, 0.0, volumetricBelowCloudsSquared);
	return normalize(float3(rayDir.x, rayDir.y + cloudRayOffset, rayDir.z));
}

CloudRaymarchSetupResult SetupCloudRaymarch(float3 worldSpaceCameraPos, float3 rayDir, float depth, float depth2)
{
	CloudRaymarchSetupResult result;

	UNITY_BRANCH
	if (_CloudPlanetRadiusVolumetric > 0.0)
	{
		result = SetupPlanetRaymarch(worldSpaceCameraPos, rayDir, depth, depth2, volumetricSphereSurface, volumetricSphereInner, volumetricSphereOutter);
	}
	else if (_WeatherMakerCloudVolumetricWeatherMapRemapBoxMin.w == 0.0)
	{
		result = SetupPlanetRaymarchBox(worldSpaceCameraPos, rayDir, depth, float2(_CloudStartVolumetric, _CloudEndVolumetric));
	}
	else
	{
		// ray march through specified box using _WeatherMakerCloudVolumetricWeatherMapRemapBoxMin and _WeatherMakerCloudVolumetricWeatherMapRemapBoxMax
		result = SetupPlanetRaymarchBoxArea(worldSpaceCameraPos, rayDir, depth, _WeatherMakerCloudVolumetricWeatherMapRemapBoxMin,
			_WeatherMakerCloudVolumetricWeatherMapRemapBoxMax);
	}

	result.cloudRayDir = rayDir;
	return result;
}

CloudRaymarchSetupResult SetupCloudRaymarchCloudRay(float3 worldSpaceCameraPos, float3 rayDir, float depth, float depth2)
{
	float3 cloudRayDir = ComputeCloudRaymarchDir(rayDir);
	CloudRaymarchSetupResult result = SetupCloudRaymarch(worldSpaceCameraPos, cloudRayDir, depth, depth2);
	result.cloudRayDir = cloudRayDir;
	return result;
}

#endif
