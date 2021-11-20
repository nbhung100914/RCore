﻿/**
 * Author RadBear - nbhung71711 @gmail.com - 2017
 **/

//#define USE_DOTWEEN

using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
#if USE_DOTWEEN
using DG.Tweening;
#endif

namespace RCore.Components
{
    public class VerticalAlignment : MyAlignment
    {
        public enum Alignment
        {
            Top,
            Bottom,
            Center,
        }

        public Alignment alignmentType;
        public float rowDistance;
        public float tweenTime = 0.25f;
        public bool autoWhenStart;
        [Range(0, 1f)] public float lerp;
        public bool moveFromRoot;
        public AnimationCurve animCurve;

        [Header("Optional")]
        public float xOffset;

        private Transform[] mChildren;
        private Vector3[] mChildrenPrePosition;
        private Vector3[] mChildrenNewPosition;
#if USE_DOTWEEN
        private Tweener mTweener;
#else
        private Coroutine mCoroutine;
#endif
        private void Start()
        {
            if (autoWhenStart)
                Align();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            Init();
            RefreshPositions();

            float t = lerp;
            if (animCurve.length > 1)
                t = animCurve.Evaluate(lerp);
            for (int j = 0; j < mChildren.Length; j++)
            {
                var pos = Vector3.Lerp(mChildrenPrePosition[j], mChildrenNewPosition[j], t);
                mChildren[j].localPosition = pos;
            }
        }

        private void Init()
        {
            var list = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.gameObject.activeSelf)
                    list.Add(transform.GetChild(i));
            }
            mChildren = list.ToArray();
        }

        private void RefreshPositions()
        {
            mChildrenPrePosition = new Vector3[mChildren.Length];
            mChildrenNewPosition = new Vector3[mChildren.Length];
            switch (alignmentType)
            {
                case Alignment.Top:
                    for (int i = 0; i < mChildrenNewPosition.Length; i++)
                    {
                        if (!moveFromRoot)
                            mChildrenPrePosition[i] = mChildren[i].localPosition;
                        mChildrenNewPosition[i] = i * new Vector3(xOffset, rowDistance, 0);
                    }
                    break;

                case Alignment.Bottom:
                    for (int i = 0; i < mChildrenNewPosition.Length; i++)
                    {
                        if (!moveFromRoot)
                            mChildrenPrePosition[i] = mChildren[i].localPosition;
                        mChildrenNewPosition[i] = (mChildrenNewPosition.Length - 1 - i) * new Vector3(xOffset, rowDistance, 0) * -1;
                    }
                    break;

                case Alignment.Center:
                    for (int i = 0; i < mChildrenNewPosition.Length; i++)
                    {
                        if (!moveFromRoot)
                            mChildrenPrePosition[i] = mChildren[i].localPosition;
                        mChildrenNewPosition[i] = i * new Vector3(xOffset, rowDistance, 0);
                    }
                    for (int i = 0; i < mChildrenNewPosition.Length; i++)
                    {
                        mChildrenNewPosition[i] = new Vector3(
                            mChildrenNewPosition[i].x + xOffset,
                            mChildrenNewPosition[i].y - mChildrenNewPosition[mChildrenNewPosition.Length - 1].y / 2,
                            mChildrenNewPosition[i].z);
                    }
                    break;
            }
        }

        public override void Align()
        {
            Init();
            RefreshPositions();
            lerp = 1;

            for (int i = 0; i < mChildren.Length; i++)
                mChildren[i].localPosition = mChildrenNewPosition[i];
        }

        public override void AlignByTweener(Action onFinish, AnimationCurve pCurve = null)
        {
            StartCoroutine(IEAlignByTweener(onFinish, pCurve));
        }

        private IEnumerator IEAlignByTweener(Action onFinish, AnimationCurve pCurve = null)
        {
            Init();
            RefreshPositions();
            if (pCurve != null)
                animCurve = pCurve;
#if USE_DOTWEEN
            bool waiting = true;
            lerp = 0;
            mTweener.Kill();
            mTweener = DOTween.To(val => lerp = val, 0f, 1f, tweenTime)
                .OnUpdate(() =>
                {
                    float t = lerp;
                    if (animCurve.length > 1)
                        t = animCurve.Evaluate(lerp);
                    for (int j = 0; j < mChildren.Length; j++)
                    {
                        var pos = Vector3.Lerp(mChildrenPrePosition[j], mChildrenNewPosition[j], t);
                        mChildren[j].localPosition = pos;
                    }
                })
                .OnComplete(() =>
                {
                    waiting = false;
                })
                .SetUpdate(true);
            if (animCurve == null)
                mTweener.SetEase(Ease.InQuint);
            while (waiting)
                yield return null;
#else
            if (mCoroutine != null)
                StopCoroutine(mCoroutine);
            mCoroutine = StartCoroutine(IEArrangeChildren(mChildrenPrePosition, mChildrenNewPosition, tweenTime));
            yield return mCoroutine;
#endif
            if (onFinish != null)
                onFinish();
        }

        private IEnumerator IEArrangeChildren(Vector3[] pChildrenPrePosition, Vector3[] pChildrenNewPosition, float pDuration)
        {
            float time = 0;
            while (true)
            {
                if (time >= pDuration)
                    time = pDuration;
                lerp = time / pDuration;
                float t = lerp;
                if (animCurve.length > 1)
                    t = animCurve.Evaluate(lerp);
                for (int j = 0; j < mChildren.Length; j++)
                {
                    var pos = Vector3.Lerp(pChildrenPrePosition[j], pChildrenNewPosition[j], t);
                    mChildren[j].localPosition = pos;
                }
                if (lerp >= 1)
                    break;
                yield return null;
                time += Time.deltaTime;
            }
        }
    }
}