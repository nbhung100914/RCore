﻿/**
 * Author RadBear - nbhung71711@gmail.com - 2017 - 2019
 **/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using RCore.Common;
using Debug = UnityEngine.Debug;
using Cysharp.Threading.Tasks;
using Random = UnityEngine.Random;

namespace RCore.Common
{
    public class SceneLoader
    {
        public static async UniTask<AsyncOperation> LoadSceneAsync(string pScene, bool pIsAdditive, bool pAutoActive, Action<float> pOnProgress, Action pOnCompleted, float pFixedLoadTime = 0, bool reLoad = false)
        {
            var scene = SceneManager.GetSceneByName(pScene);
            if (scene.isLoaded && !reLoad)
            {
                pOnProgress.Raise(1);
                pOnCompleted.Raise();
                return null;
            }

            var sceneOperator = SceneManager.LoadSceneAsync(pScene, pIsAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
            sceneOperator.allowSceneActivation = false;
            await ProcessOperationAsync(sceneOperator, pAutoActive, pOnProgress, pOnCompleted, pFixedLoadTime);
            return sceneOperator;
        }

        public static void LoadScene(string pScene, bool pIsAdditive, bool reLoad = false)
        {
            var scene = SceneManager.GetSceneByName(pScene);
            if (scene.isLoaded && !reLoad)
                return;

            SceneManager.LoadScene(pScene, pIsAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
        }

        private static async UniTask ProcessOperationAsync(AsyncOperation sceneOperator, bool pAutoActive, Action<float> pOnProgress, Action pOnCompleted, float pFixedLoadTime = 0)
        {
            pOnProgress.Raise(0f);

            float startTime = Time.unscaledTime;
            float fakeProgress = Random.Range(0.2f, 0.4f);
            float offsetProgress = pFixedLoadTime > 0 ? fakeProgress : 0;

            while (true)
            {
                float progress = Mathf.Clamp01(sceneOperator.progress / 0.9f);
                pOnProgress.Raise(Mathf.Clamp01(progress - offsetProgress));
                await UniTask.Yield();

                if (sceneOperator.isDone || progress >= 1)
                    break;
            }

            float loadTime = Time.unscaledTime - startTime;
            float additionalTime = pFixedLoadTime - loadTime;
            if (additionalTime <= 0)
                pOnProgress.Raise(1);
            else
            {
                float time = 0;
                while (true)
                {
                    time += Time.deltaTime;
                    if (time > additionalTime)
                        break;

                    float progress = (1 - fakeProgress) + time / additionalTime * fakeProgress;
                    pOnProgress.Raise(Mathf.Clamp01(progress));
                    await UniTask.Yield();
                }
                pOnProgress.Raise(1);
            }

            pOnCompleted.Raise();

            if (pAutoActive)
                sceneOperator.allowSceneActivation = true;
        }

        public static async UniTask<AsyncOperation> UnloadSceneAsync(string pScene, Action<float> pOnProgress, Action pOnComplted)
        {
            var scene = SceneManager.GetSceneByName(pScene);
            if (!scene.isLoaded)
            {
                pOnProgress(1f);
                return null;
            }

            var sceneOperator = SceneManager.UnloadSceneAsync(pScene);
            await ProcessOperationAsync(sceneOperator, false, pOnProgress, pOnComplted);
            return sceneOperator;
        }

        public static async UniTask<AsyncOperation> UnloadScene(Scene pScene, Action<float> pOnProgress, Action pOnComplted)
        {
            if (!pScene.isLoaded)
            {
                pOnProgress(1f);
                return null;
            }

            var sceneOperator = SceneManager.UnloadSceneAsync(pScene);
            await ProcessOperationAsync(sceneOperator, false, pOnProgress, pOnComplted);
            return sceneOperator;
        }
    }
}