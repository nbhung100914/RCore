/**
 * Author RadBear - nbhung71711@gmail.com - 2018
 **/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCore.Pattern.Data
{
    public abstract class FunData
    {
        #region Members

        /// <summary>
        /// Unique of id of data in it's group
        /// </summary>
        protected int m_Id;
        /// <summary>
        /// Name for better search
        /// </summary>
        protected string m_Alias;
        /// <summary>
        /// Key of data, used to load data from data list
        /// </summary>
        protected string m_Key;
        /// <summary>
        /// Cached index of saved data, used to quick get/set
        /// </summary>
        protected int m_Index = -1;
        /// <summary>
        /// Id String of data saver
        /// </summary>
        protected string m_SaverIdString;

        public string Key { get { return m_Key; } }
        public int Id { get { return m_Id; } }
        public int Index { get { return m_Index; } }
        public DataSaver DataSaver { get { return DataSaverContainer.GetSaver(m_SaverIdString); } }

        public virtual List<FunData> Children { get { return null; } }

        #endregion

        //=======================================

        #region Common Methods

        public FunData(int pId, string pAlias)
        {
            m_Id = pId;
            m_Alias = pAlias;
        }

        /// <summary>
        /// Build Key used in Data Saver
        /// </summary>
        /// <param name="pBaseKey">Key of Data Group (Its parent)</param>
        /// <param name="pSaverIdString"></param>
        public virtual void Load(string pBaseKey, string pSaverIdString)
        {
            Debug.Assert(pSaverIdString != null, "Data saver cannot be null!");

            if (!string.IsNullOrEmpty(pBaseKey))
                m_Key = string.Format("{0}.{1}", pBaseKey, m_Id);
            else
                m_Key = m_Id.ToString();
            m_SaverIdString = pSaverIdString;
        }
        public virtual void PostLoad() { }
        public virtual void SetStringValue(string pValue)
        {
            try
            {
                if (m_Index == -1)
                    m_Index = DataSaver.Set(m_Key, pValue, m_Alias);
                else
                    DataSaver.Set(m_Index, pValue, m_Alias);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        }
        public virtual string GetStringValue()
        {
            if (m_Index == -1)
                return DataSaver.Get(m_Key, out m_Index);
            else
                return DataSaver.Get(m_Index);
        }
        public virtual void ClearIndex()
        {
            m_Index = -1;
        }
        public virtual void OnApplicationPaused(bool pPaused) { }
        public virtual void OnApplicationQuit() { }
        /// <summary>
        /// Reload data back to last saved
        /// </summary>
        /// <param name="pClearIndex">Clear cached index of data in data list</param>
        public virtual void Reload(bool pClearIndex)
        {
            if (pClearIndex)
                m_Index = -1;
        }
        public abstract void Reset();
        public abstract bool Stage();
        public abstract bool Cleanable();

        #endregion
    }
}