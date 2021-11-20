﻿// Author - NBear - nbhung71711@gmail.com - 2020

//#define USE_DOTWEEN
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RCore.Common;
using UnityEditor;
using Debug = UnityEngine.Debug;
using RCore.Inspector;
#if USE_DOTWEEN
using DG.Tweening;
#endif

namespace RCore.Components
{
    /// <summary>
    /// Used to replace CustomSizeFilter of Unity
    /// </summary>
    public class ContentRectSizeFilter : MonoBehaviour
    {
        #region Members

#pragma warning disable 0649
        [SerializeField] private RectTransform m_Content;
        [SerializeField] private List<RectTransform> m_Children;
        [SerializeField] private Vector2 m_ContentSizeBonus;
        [Separator("Movement")]
        [SerializeField] private float m_mMinTweenTime = 0.25f;
        [SerializeField] private float m_mMaxTweenTime = 0.75f;
        [SerializeField] private float m_TransitionSpeed = 10;
        [SerializeField] private int m_Index;
        [SerializeField] private Vector2 m_ChildPosOffset;
#pragma warning restore 0649

        [SerializeField, ReadOnly] private Vector2 m_ChildTopRight;
        [SerializeField, ReadOnly] private Vector2 m_ChilBotLeft;

        #endregion

        //=============================================

        #region MonoBehaviour

        private void OnValidate()
        {
            if (m_Content == null)
                m_Content = transform as RectTransform;
        }

        #endregion

        //=============================================

        #region Public

        public void AutoSize()
        {
            if (Application.isPlaying)
            {
                if (m_Children == null || m_Children.Count == 0)
                {
                    m_Children = new List<RectTransform>();
                    foreach (RectTransform child in m_Content)
                        m_Children.Add(child);
                }
            }
            else
            {
                m_Children = new List<RectTransform>();
                foreach (RectTransform child in m_Content)
                    m_Children.Add(child);
            }

            m_ChildTopRight = Vector2.zero;
            m_ChilBotLeft = Vector2.zero;
            for (int i = 0; i < m_Children.Count; i++)
            {
                var topRight = m_Children[i].TopRight();
                if (topRight.x > m_ChildTopRight.x)
                    m_ChildTopRight.x = topRight.x;
                if (topRight.y > m_ChildTopRight.y)
                    m_ChildTopRight.y = topRight.y;

                var botLeft = m_Children[i].BotLeft();
                if (botLeft.x < m_ChilBotLeft.x)
                    m_ChilBotLeft.x = botLeft.x;
                if (botLeft.y < m_ChilBotLeft.y)
                    m_ChilBotLeft.y = botLeft.y;
            }

            float height = m_ChildTopRight.y - m_ChilBotLeft.y + m_ContentSizeBonus.y;
            float width = m_ChildTopRight.x - m_ChilBotLeft.x + m_ContentSizeBonus.x;

            m_Content.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// NOTE: AnchorMin, AnchorMax and Pivot of children must be 0.5, 0.5
        /// </summary>
        public void RepositionChildren()
        {
            AutoSize();

            float widght = m_Content.rect.width;
            float height = m_Content.rect.height;

            Vector2 parentTopRight = new Vector2(m_Content.rect.width * (1 - m_Content.pivot.x), m_Content.rect.height * (1 - m_Content.pivot.y));
            float offsetY = m_ChildTopRight.y - parentTopRight.y + m_ContentSizeBonus.y / 2f;
            float offsetX = m_ChildTopRight.x - parentTopRight.x + m_ContentSizeBonus.x / 2f;
            for (int i = 0; i < m_Children.Count; i++)
            {
                var childPos = m_Children[i].anchoredPosition;
                childPos.x -= offsetX - ((m_Content.pivot.x - 0.5f) * widght);
                childPos.y -= offsetY - ((m_Content.pivot.y - 0.5f) * height);
                m_Children[i].anchoredPosition = childPos;
            }

            //Recalculate top-right and bot-left
            AutoSize();
        }

        public void MoveContentToChild(int pIndex, bool pSmooth = false)
        {
            if (pIndex < 0 || m_Children.Count <= pIndex)
            {
                Debug.LogError("Index is invalid");
                return;
            }

            m_Index = pIndex;

            float width = m_Content.rect.width;
            float height = m_Content.rect.height;
            var childRect = m_Children[pIndex];
            var targetAnchoredPos = childRect.CovertAnchoredPosFromChildToParent(m_Content);
            targetAnchoredPos.y -= m_ChildPosOffset.y;
            targetAnchoredPos.x -= m_ChildPosOffset.x;
#if UNITY_EDITOR
            Debug.Log("targetAnchoredPos: " + targetAnchoredPos);
#endif
            if (!Application.isPlaying || !pSmooth)
            {
                m_Content.anchoredPosition = targetAnchoredPos;
            }
            else
            {
#if USE_DOTWEEN
                Vector2 fromPos = m_Content.anchoredPosition;

                float time = Vector2.Distance(targetAnchoredPos, fromPos) / (m_TransitionSpeed / Time.deltaTime);
                if (time == 0)
                    return;
                if (time < m_mMinTweenTime)
                    time = m_mMinTweenTime;
                else if (time > m_mMaxTweenTime)
                    time = m_mMaxTweenTime;

                DOTween.Kill(GetInstanceID());
                m_Content.DOAnchorPosY(targetAnchoredPos.y, time)
                    .OnComplete(() =>
                    {
                        m_Content.anchoredPosition = targetAnchoredPos;
                    }).SetId(GetInstanceID());
#else
                m_Content.anchoredPosition = targetAnchoredPos;
#endif
            }
        }

        #endregion

        //==============================================

        #region Private

        #endregion

#if UNITY_EDITOR
        [CustomEditor(typeof(ContentRectSizeFilter))]
        private class AutoSizeRectransformEditor : Editor
        {
            private ContentRectSizeFilter m_Target;

            private void OnEnable()
            {
                m_Target = target as ContentRectSizeFilter;
            }

            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                if (EditorHelper.Button("Calculate Size"))
                {
                    m_Target.AutoSize();
                }
                if (EditorHelper.Button("Re-position children"))
                {
                    m_Target.RepositionChildren();
                }
                if (EditorHelper.Button("Move to index"))
                {
                    m_Target.MoveContentToChild(m_Target.m_Index);
                }
                if (EditorHelper.Button("Move to index with tween"))
                {
                    m_Target.MoveContentToChild(m_Target.m_Index, true);
                }
            }
        }
#endif
    }
}