﻿/**
 *  Based on TimeManager of hnim.
 *  Copyright (c) 2017 RedAntz. All rights reserved.
 */

using System;
using RCore.Common;

namespace RCore.Pattern.Data
{
    public class TimerTask : DataGroup
    {
        private Action<TimerTask, long> m_OnCompleted;

        private LongData m_RemainSeconds;
        private LongData m_LocalSeconds;
        private LongData m_ServerRemainSeconds;
        private LongData m_ServerSeconds;
        private LongData m_TaskDurationSeconds;

        public long RemainSeconds { get { return m_RemainSeconds.Value; } }
        public long Seconds { get { return m_LocalSeconds.Value; } }
        public long TaskDurationSeconds { get { return m_TaskDurationSeconds.Value; } }
        public bool IsRunning { get { return m_RemainSeconds.Value > 0; } }

        public TimerTask(int pId) : base(pId)
        {
            m_RemainSeconds = AddData(new LongData(0));
            m_LocalSeconds = AddData(new LongData(1));
            m_ServerRemainSeconds = AddData(new LongData(2));
            m_ServerSeconds = AddData(new LongData(3));
            m_TaskDurationSeconds = AddData(new LongData(4));
        }

        public override void Load(string pBaseKey, string pSaverIdString)
        {
            base.Load(pBaseKey, pSaverIdString);

            if (IsRunning)
                TimerTaskManager.Instance.AddTimerTask(this);
        }

        public void SetOnComplete(Action<TimerTask, long> pAction)
        {
            m_OnCompleted = pAction;
        }

        private void Finished()
        {
            var remainSeconds = m_RemainSeconds.Value;

            Stop();

            if (m_OnCompleted != null)
                m_OnCompleted(this, remainSeconds);
        }

        public void Stop()
        {
            m_RemainSeconds.Value = 0;
            m_LocalSeconds.Value = 0;
            m_ServerRemainSeconds.Value = 0;
            m_ServerSeconds.Value = 0;
        }

        public void PassSeconds(long pPassSeconds)
        {
            if (!IsRunning)
                return;

            m_RemainSeconds.Value -= pPassSeconds;
            m_ServerRemainSeconds.Value -= pPassSeconds;

            if (!IsRunning)
                Finished();
        }

        public void AddSeconds(long pSeconds)
        {
            if (IsRunning)
            {
                m_RemainSeconds.Value += pSeconds;
                m_ServerRemainSeconds.Value += pSeconds;
            }
        }

        public void Start(long pSecondsDurations)
        {
            m_TaskDurationSeconds.Value = pSecondsDurations;
            m_RemainSeconds.Value = pSecondsDurations;
            m_ServerRemainSeconds.Value = pSecondsDurations;

            var timeManager = TimerTaskManager.Instance;
            long curServerSeconds = timeManager.GetCurrentServerSeconds();
            long curLocalSeconds = timeManager.GetSecondsSinceBoot();
            m_ServerSeconds.Value = curServerSeconds;
            m_LocalSeconds.Value = curLocalSeconds;
            timeManager.AddTimerTask(this);
        }

        public void Update(long pCurServerSeconds, long pCurLocalSeconds, long pDeltaOnlineSeconds)
        {
            if (IsRunning)
            {
                if (pCurServerSeconds > 0)
                {
                    //If server time was saved, count directly, otherwise record it
                    long dServerSeconds = pCurServerSeconds - m_ServerSeconds.Value;// this value should never be negative
                    if (m_ServerSeconds.Value > 0 && dServerSeconds > 0)
                    {
                        m_ServerRemainSeconds.Value -= dServerSeconds;
                        m_ServerSeconds.Value = pCurServerSeconds;

                        m_LocalSeconds.Value = pCurLocalSeconds;
                        m_RemainSeconds.Value = m_ServerRemainSeconds.Value;
                    }
                    else
                    {
                        m_ServerSeconds.Value = pCurServerSeconds;
                        m_ServerRemainSeconds.Value = m_RemainSeconds.Value;
                    }
                }
                //
                if (m_LocalSeconds.Value <= 0)
                {
                    m_LocalSeconds.Value = pCurLocalSeconds;
                }
                //
                long dt = pCurLocalSeconds - m_LocalSeconds.Value;
                //
                if (dt < 0)
                {
                    // means user turn off device then switch on
                    dt = pCurLocalSeconds;
                }

                if (dt > 0)
                {
                    m_LocalSeconds.Value = pCurLocalSeconds;
                    m_RemainSeconds.Value -= dt;
                }

                // check if finished
                if (!IsRunning)
                    Finished();
            }
        }

        public void SetRemainSeconds(long pValue)
        {
            if (!IsRunning)
                return;

            m_TaskDurationSeconds.Value = pValue;
            m_RemainSeconds.Value = pValue;
            m_ServerRemainSeconds.Value = pValue;

            if (!IsRunning)
                Finished();
        }
    }
}