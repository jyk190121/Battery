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

#ifndef __WEATHER_MAKER_CLOUD_VOLUMETRIC_RAYMARCH_SHADER__
#define __WEATHER_MAKER_CLOUD_VOLUMETRIC_RAYMARCH_SHADER__

#include "WeatherMakerCloudVolumetricSamplingShaderInclude.cginc"
#include "WeatherMakerCloudVolumetricRaymarchSetupShaderInclude.cginc"
#include "WeatherMakerCloudVolumetricLightingShaderInclude.cginc"
#include "WeatherMakerCloudVolumetricAtmosphereShaderInclude.cginc"
#include "WeatherMakerCoreShaderInclude.cginc"

float PrecomputeCloudVolumetricHenyeyGreensteinVolumetric(DirLightPrecomputation dirLight)
{

#define VOLUMETRIC_MAX_HENYEY_GREENSTEIN 5.0

	// https://www.diva-portal.org/smash/get/diva2:1223894/FULLTEXT01.pdf
	// f(x) = (1 - g)^2 / (4PI * (1 + g^2 - 2g*cos(x))^[3/2])
	// _CloudHenyeyGreensteinPhase.x = forward, _CloudHenyeyGreensteinPhase.y = back
	static const float g = _CloudHenyeyGreensteinPhaseVolumetric.x;
	static const float gSquared = g * g;
	static const float oneMinusGSquared = (1.0 - gSquared);
	static const float onePlusGSquared = 1.0 + gSquared;
	static const float twoG = 2.0 * g;
	float falloff = pow(PI * (onePlusGSquared - (twoG * dirLight.eyeDot)), 1.5);
	float forward = oneMinusGSquared / falloff;

	static const float g2 = _CloudHenyeyGreensteinPhaseVolumetric.y;
	static const float gSquared2 = g2 * g2;
	static const float oneMinusGSquared2 = (1.0 - gSquared2);
	static const float onePlusGSquared2 = 1.0 + gSquared2;
	static const float twoG2 = 2.0 * g2;
	float falloff2 = pow(PI * (onePlusGSquared2 - (twoG2 * dirLight.eyeDot)), 1.5);
	float back = oneMinusGSquared2 / falloff2;

	// hg back lighting is more dim than hg forward light as light intensity goes below 1
	return min(VOLUMETRIC_MAX_HENYEY_GREENSTEIN, (((forward * _CloudHenyeyGreensteinPhaseVolumetric.z) + (back * dirLight.intensity * _CloudHenyeyGreensteinPhaseVolumetric.w))));
}

DirLightPrecomputation PrecomputeDirLight(in CloudState state, float3 rayDir, uint lightIndex)
{
	UNITY_BRANCH
	if (_WeatherMakerDirLightColor[lightIndex].a > 0.0)
	{
		fixed3 lightColor = _WeatherMakerDirLightColor[lightIndex].rgb;
		float3 lightDir = _WeatherMakerDirLightPosition[lightIndex].xyz;
		lightDir.y = max(0.1, lightDir.y);

		float intensity = min(1.0, _WeatherMakerDirLightColor[lightIndex].a);
		float intensitySquared = intensity * intensity;
		float eyeDot = dot(rayDir, lightDir);
		DirLightPrecomputation item;
		item.eyeDot = eyeDot;
		float energy = max(_WeatherMakerDirLightColor[lightIndex].a, max(0.33, (eyeDot + 1.0) * 0.5) * _WeatherMakerDirLightVar1[lightIndex].w) * _CloudDirLightMultiplierVolumetric;
		item.intensity = intensity;
		item.intensitySquared = intensitySquared;
		item.hg = PrecomputeCloudVolumetricHenyeyGreensteinVolumetric(item) * energy;
		item.lightDir = lightDir;
		float powderEyeDot = ((eyeDot * -0.5) + 0.5);
		item.powderMultiplier = lerp(1.0, _CloudPowderMultiplierVolumetric, min(1.0, 4.0 * intensity * lightDir.y * lightDir.y));
		item.powderAngle = min(1.0, _CloudPowderMultiplierVolumetric * powderEyeDot * intensity) * min(1.0, _CloudPowderMultiplierVolumetric);
		item.shadowPower = (1.0 - lightDir.y);
		item.shadowPower *= item.shadowPower;
		item.shadowPower += 0.5;
		item.shadowPower *= _CloudLightAbsorptionVolumetric;
		item.lightConeRadius = state.lightStepSize * _CloudLightRadiusMultiplierVolumetric * clamp(lightDir.y * 2.5, 0.1, 1.0);
		item.indirectLight = item.intensity * lightColor * _CloudDirLightIndirectMultiplierVolumetric;
		return item;
	}
	else
	{
		return emptyDirLight;
	}
}

CloudState PrecomputeCloudState(float3 rayDir, float2 uv)
{
	CloudState state;
	state.dithering = tex2Dlod(_WeatherMakerBlueNoiseTexture, float4(uv + _WeatherMakerTemporalUV_FragmentShader, 0.0, 0.0));
	state.lightStepSize = volumetricDirLightStepSize * (1.0 + (state.dithering * 0.015));
	state.lightColorDithering = (state.dithering * 0.005);
	state.fade = 0.0;

	UNITY_UNROLL
	for (uint lightIndex = 0; lightIndex < uint(MAX_LIGHT_COUNT); lightIndex++)
	{
		state.dirLight[lightIndex] = PrecomputeDirLight(state, rayDir, lightIndex);
	}

	return state;
}

fixed4 RaymarchVolumetricClouds
(
	float3 rayOrigin,
	float3 marchPos,
	float3 endPos,
	float rayLength,
	float distanceToCloud,
	float3 rayDir,
	float3 origRayDir,
	float4 uv,
	float depth,
	fixed3 skyColor,
	in CloudState state,
	inout uint marches,
	out fixed horizonFade
)
{
	float startOpticalDepth = min(1.0, distanceToCloud * invVolumetricMaxOpticalDistance);
	horizonFade = ComputeCloudColorVolumetricHorizonFade(startOpticalDepth);

	// if no night multiplier, we can early exit as the sky will be mapped to this pixel
	UNITY_BRANCH
	if (_WeatherMakerNightMultiplier == 0.0 && volumetricIsAboveClouds == 0.0 && horizonFade < 0.001)
	{
		return fixed4Zero;
	}
	else
    {
        fixed4 cloudColor = fixed4Zero;
        uint i = 0;
        float skyAmbientMultiplier = 1.0; // clamp(startOpticalDepth * VOLUMETRIC_SKY_AMBIENT_OPTICAL_DEPTH_MULTIPLIER, 0.5, 1.0);
        float absRayY = abs(rayDir.y);
        uint sampleCount = uint(lerp(volumetricSampleCountRange.y, volumetricSampleCountRange.x, absRayY));
        float invSampleCount = 1.0 / float(sampleCount);
	//float ditherRay = abs(RandomFloat(marchPos)) * lerp(_CloudRayDitherVolumetric.y, _CloudRayDitherVolumetric.x, pow(absRayY, 1.5));
        float ditherRay = state.dithering.b * lerp(_CloudRayDitherVolumetric.y, _CloudRayDitherVolumetric.x, pow(absRayY, 1.5));

	// if ray-march x or y is less than 1, it is considered a percentage of the ray length step unit
        float marchLengthStepMultiplierPercent = rayLength * invSampleCount * _CloudRaymarchMultiplierVolumetric;
        float marchLength = lerp((marchLengthStepMultiplierPercent * _CloudRayMarchParameters.x), _CloudRaymarchMultiplierVolumetric * _CloudRayMarchParameters.x, _CloudRayMarchParameters.x > 1.0);
        float marchLengthFull = lerp((marchLengthStepMultiplierPercent * _CloudRayMarchParameters.y), _CloudRaymarchMultiplierVolumetric * _CloudRayMarchParameters.y, _CloudRayMarchParameters.y > 1.0);
        marchLength = clamp(marchLength, VOLUMETRIC_MIN_STEP_LENGTH, VOLUMETRIC_MAX_STEP_LENGTH);
        marchLengthFull = clamp(marchLengthFull, VOLUMETRIC_MIN_STEP_LENGTH, VOLUMETRIC_MAX_STEP_LENGTH);

        float3 marchDirLong = (rayDir * marchLengthFull);
        float3 marchDirShort = (rayDir * marchLength);
        float3 marchDir = float3Zero;
        marchPos += (rayDir * ditherRay * 256.0);
        float heightFrac = 0.0;
        float cloudSample = 0.0;
        float cloudSampleTotal = 0.0;
        float4 lightSample = float4Zero;
        float4 weatherData = float4Zero;
        float marchLerp = 0.0;
        float3 t = float3Zero, s = float3Zero;
        bool sampled = false;

	// increase lod for clouds that are farther away, with distance-squared falloff for distant clouds
        float lod = min(volumetricLod.y, volumetricLod.x + (startOpticalDepth * VOLUMETRIC_LOD_OPTICAL_DEPTH_MULTIPLIER) + (startOpticalDepth * startOpticalDepth * VOLUMETRIC_LOD_DISTANCE_MULTIPLIER));
        float marchLerpPower = lerp(_CloudRayMarchParameters.z, _CloudRayMarchParameters.w, startOpticalDepth * startOpticalDepth);
        uint marchLerpIndex = 0;
        float sdf = 0.0;
        float3 sdfDir = (rayDir * _WeatherMakerWeatherMapScale.w) / max(0.01, length(rayDir.xz));
        float marchMultiplier = 1.0;

#if defined(VOLUMETRIC_CLOUD_ENABLE_AMBIENT_SKY_DENSITY_SAMPLE)

	float3 ambientPos = float3Zero;

#endif

        UNITY_LOOP
        while (i++ < sampleCount && cloudColor.a < VOLUMETRIC_CLOUD_MAX_ALPHA && heightFrac >= -0.01 && heightFrac <= 1.01)
        {
            heightFrac = GetCloudHeightFractionForPoint(marchPos);

            UNITY_BRANCH
            if (heightFrac <= _CloudTypeVolumetric)
            {
                weatherData = CloudVolumetricSampleWeather(marchPos + (volumetricWindDir2 * heightFrac), heightFrac, lod);

				// min coverage
                UNITY_BRANCH
                if (CloudVolumetricGetCoverage(weatherData) > _CloudCoverVolumetricMinimumForCloud)
                {
                    cloudSample = SampleCloudDensity(marchPos, weatherData, heightFrac, lod, false, sampled);

					// soft particles
                    UNITY_BRANCH
                    if (cloudSample > VOLUMETRIC_CLOUD_MIN_NOISE_VALUE && depth < _ProjectionParams.z)
                    {
                        float partZ = distance(marchPos, rayOrigin);
                        float diff = (depth - partZ);
                        float multiplier = saturate(_CloudInvFade * diff);

						// if we have gotten close enough or beyond the depth buffer, we are done
                        i = lerp(sampleCount, i, multiplier > 0.001);

						// adjust cloud sample
                        cloudSample *= multiplier;
                    }

					// denote expensive march performed
                    marches += sampled;

					// march at reduced march speed when maybe in cloud
                    marchMultiplier = _CloudRaymarchMaybeInCloudStepMultiplier;

                    UNITY_BRANCH
                    if (cloudSample > VOLUMETRIC_DETAIL_MIN_NOISE_VALUE)
                    {
						// sample just details using the shape noise from the above call which was done without details
                        cloudSample = SampleCloudDensityDetails(cloudSample, marchPos, heightFrac, weatherData, lod);

						// do we still have a cloud?
                        UNITY_BRANCH
                        if (cloudSample > VOLUMETRIC_CLOUD_MIN_NOISE_VALUE)
                        {
							// march at reduced march speed when in cloud
                            marchMultiplier = _CloudRaymarchInCloudStepMultiplier;
                            cloudSampleTotal += cloudSample;
                            lightSample.rgb = SampleAmbientLight(marchPos, rayDir, rayLength, skyAmbientMultiplier, skyColor, heightFrac, weatherData);
                            lightSample.rgb += SampleDirLightSources(marchPos, rayDir, heightFrac, cloudSample, cloudSampleTotal, lod, state);
                            lightSample.rgb += SamplePointLightSources(marchPos, rayDir, heightFrac, cloudSample, cloudSampleTotal, lod, uv);
                            lightSample.a = ComputeVolumetricCloudStepAlpha(cloudColor.a, cloudSample);

							/*
							float depth01 = distance(rayOrigin, marchPos) * atmosphere01;
							s = UNITY_SAMPLE_TEX3D_LOD(_WeatherMakerInscatteringLUT, float3(uv.x, uv.y, depth01), 0.0);
							t = UNITY_SAMPLE_TEX3D_LOD(_WeatherMakerExtinctionLUT, float3(uv.x, uv.y, depth01), 0.0);
							lightSample.rgb *= lerp(t, fixed3One, state.fade);
							lightSample.rgb += s;
							*/

                            lightSample.rgb *= lightSample.a;

							// accumulate color using the same alpha contribution returned above
                            cloudColor.rgb += lightSample.rgb;
                            cloudColor.a += lightSample.a;
                        }
                    }
                }
                else if ((sdf = CloudVolumetricGetDistance(weatherData)) < 0.99)
                {
					// flip back to pixel space, protect against 0 sdf values
                    sdf = round(1.0 / max(sdf, 0.01));

					// march to next sdf position
                    marchPos += (sdf * sdfDir);
                    marchLerpIndex += uint(sdf); // advance step progression during SDF skip
                }
            }

			// increase march based on march index and power
            marchLerp = pow(saturate(float(marchLerpIndex++) * invSampleCount), marchLerpPower);
            marchDir = lerp(marchDirShort, marchDirLong, marchLerp);
            marchPos += (marchDir * marchMultiplier);
            marchMultiplier = 1.0;
        }

		// tidy up last 0.01 of alpha
        cloudColor.a = min(1.0, cloudColor.a * VOLUMETRIC_CLOUD_MAX_ALPHA_INV);
	
        UNITY_BRANCH
        if (horizonFade > 0.0)
        {
			// reduce horizon fade for bright values, these cut through the sky scattering better, think white parts of clouds at horizon
            fixed cloudLuminosity = min(1.0, Luminance(cloudColor.rgb * cloudColor.a));

			// reduce luminosity power
            cloudLuminosity = pow(cloudLuminosity, _CloudHorizonFadeVolumetric.x);

			// bulk back up for any luminosity that remains
            cloudLuminosity *= 1.5;

			// luminosity fights through horizon fade
            horizonFade = lerp(horizonFade, 1.0, min(1.0, horizonFade * cloudLuminosity));
        }

        return cloudColor;
    }
}

CloudColorResult ComputeCloudColorVolumetric(float3 rayOrigin, float3 rayDir, float4 uv, float depth, float depth01, in CloudState state)
{
	// determine what (if any) part of the cloud volume we intersected
	CloudRaymarchSetupResult raymarchSetup = SetupCloudRaymarchCloudRay(rayOrigin, rayDir, depth, depth);
	float3 cloudRayDir = raymarchSetup.cloudRayDir;
	fixed tmpHorizonFade;
	uint iterations = raymarchSetup.iterations;

	UNITY_BRANCH
	if (iterations > 0)
	{
		iterations = (volumetricIsAboveMiddleClouds ? 1 : iterations);
		float horizonFade = 1.0;
		fixed4 cloudLightColors[2] = { fixed4Zero, fixed4Zero };
		uint marches = 0;
		fixed3 skyColor = volumetricCloudAmbientColorSky;

		UNITY_LOOP
		for (uint iterationIndex = 0; iterationIndex < iterations; iterationIndex++)
		{
			cloudLightColors[iterationIndex] = RaymarchVolumetricClouds
			(
				rayOrigin,
				lerp(raymarchSetup.startPos, raymarchSetup.startPos2, iterationIndex),
				lerp(raymarchSetup.endPos, raymarchSetup.endPos2, iterationIndex),
				lerp(raymarchSetup.rayLength, raymarchSetup.rayLength2, iterationIndex),
				lerp(raymarchSetup.distanceToSphere, raymarchSetup.distanceToSphere2, iterationIndex),
				cloudRayDir,
				rayDir,
				uv,
				depth,
				skyColor,
				state,
				marches,
				tmpHorizonFade
			);

			// if we hit back half of sphere, reduce horizon fade by the front part alpha
			horizonFade = (iterationIndex == 0 ? tmpHorizonFade : lerp(tmpHorizonFade, horizonFade, cloudLightColors[0].a));

			// if we have enough cloud, exit the loop
			iterationIndex = lerp(iterationIndex, 2, cloudLightColors[iterationIndex].a >= 0.999);
		}

		// custom blend
		cloudLightColors[1].rgb = (cloudLightColors[0].rgb + (cloudLightColors[1].rgb * (1.0 - cloudLightColors[0].a)));
		cloudLightColors[1].a = max(cloudLightColors[0].a, cloudLightColors[1].a);
		fixed4 finalColor = FinalizeVolumetricCloudColor(cloudLightColors[1] * _CloudColorVolumetric, uv, marches);
		CloudColorResult result = { finalColor, horizonFade, 1.0 };
		return result;
	}
	else
	{
		// missed cloud layer entirely
		CloudColorResult result = { fixed4Zero, 1.0, 0.0 };
		return result;
	}
}

CloudColorResult ComputeCloudColorVolumetric(float3 rayDir, float4 uv, float depth, float depth01, inout CloudState state)
{
	return ComputeCloudColorVolumetric(WEATHER_MAKER_CLOUD_CAMERA_POS, rayDir, uv, depth, depth01, state);
}

CloudColorResult ComputeCloudColorAll(float3 rayOrigin, float3 rayDir, float4 uv, float depth, float depth01, fixed4 backgroundSkyColor, inout CloudState state)
{
	float hitCloud = 0.0;
	fixed4 finalColor = fixed4Zero;

	CloudColorResult flatColor = ComputeFlatCloudColorAll(rayDir, depth, uv, _CloudNoiseLod, state);
	hitCloud = flatColor.hitCloud;
	finalColor = flatColor.color;

	UNITY_BRANCH
	if (_CloudCoverVolumetric > 0.0)
	{
		CloudColorResult volumetricColor = ComputeCloudColorVolumetric(rayOrigin, rayDir, uv, depth, depth01, state);
		hitCloud = min(1.0, hitCloud + volumetricColor.hitCloud);
		fixed horizonFade = volumetricColor.fade;
		finalColor = volumetricColor.color + (finalColor * (1.0 - volumetricColor.color.a));

#if VOLUMETRIC_CLOUD_RENDER_MODE == 1

		UNITY_BRANCH
		if (horizonFade < 1.0 && finalColor.a > 0.0)
		{
			UNITY_BRANCH
			if (volumetricIsAboveClouds)
			{
				finalColor *= horizonFade;
			}
			else
			{
				finalColor.rgb *= horizonFade;
				finalColor.rgb += (backgroundSkyColor.rgb * (1.0 - horizonFade) * finalColor.a);
			}
		}

#endif

	}

	CloudColorResult result = { finalColor, 1.0, hitCloud };
	return result;
}

#endif // __WEATHER_MAKER_CLOUD_VOLUMETRIC_RAYMARCH_SHADER__
