﻿/**
 * Author RadBear - nbhung71711 @gmail.com - 2019
 **/

#if ACTIVE_FIREBASE_STORAGE
using Firebase;
using Firebase.Storage;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using RCore.Common;
using Debug = UnityEngine.Debug;

namespace RCore.Service
{
    public class WaitForTaskStorage : CustomYieldInstruction
    {
        Task task;

        public WaitForTaskStorage(Task task)
        {
            this.task = task;
        }

        public override bool keepWaiting
        {
            get
            {
                if (task.IsCompleted)
                {
                    if (task.IsFaulted)
                        LogException(task.Exception);

                    return false;
                }
                return true;
            }
        }

        private void LogException(Exception exception)
        {
#if ACTIVE_FIREBASE_STORAGE
            var storageException = exception as StorageException;
            if (storageException != null)
            {
                Debug.LogError(string.Format("[storage]: Error Code: {0}", storageException.ErrorCode));
                Debug.LogError(string.Format("[storage]: HTTP Result Code: {0}", storageException.HttpResultCode));
                Debug.LogError(string.Format("[storage]: Recoverable: {0}", storageException.IsRecoverableException));
                Debug.LogError("[storage]: " + storageException.ToString());
            }
            else
#endif
                Debug.LogError(exception.ToString());
        }
    }

    //=======================================================

    public class SavedFileDefinition
    {
        public string rootFolder { get; private set; }
        public string fileName { get; private set; }
        public string metaData { get; private set; }

        public SavedFileDefinition(string pRootFolder, string pFileName, Dictionary<string, string> metaDataDict = null)
        {
            fileName = pFileName;
            rootFolder = pRootFolder;
            metaData = BuildMetaData(metaDataDict);
        }

        public string BuildMetaData(Dictionary<string, string> pMetaDataDict)
        {
            List<string> build = new List<string>();
            foreach (var metaData in pMetaDataDict)
            {
                string key = metaData.Key;
                string value = metaData.Value;
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    build.Add(string.Format("{0}={1}", key, value));
            }
            return string.Join("\\", build.ToArray());
        }

        public string GetStorageLocation(string pStorageBucket)
        {
            string folder = string.Format("{0}/{1}", pStorageBucket, rootFolder);
            return string.Format("{0}/{1}", folder, fileName);
        }
    }

    //=======================================================

    public static class RFirebaseStorage
    {
#if ACTIVE_FIREBASE_STORAGE

        #region Constants

        private static readonly string URI_FILE_SCHEME = Uri.UriSchemeFile + "://";

        #endregion

        //=======================================================

        #region Members

        private static string m_StorageBucket;
        private static bool m_IsDownloading;
        private static bool m_IsUploading;
        private static bool m_IsDeleting;
        private static bool m_Initialized;

        public static bool Initialized { get { return m_Initialized; } }

        /// <summary>
        /// Cancellation token source for the current operation.
        /// </summary>
        private static CancellationTokenSource m_CancellationTokenSource = new CancellationTokenSource();

        #endregion

        //=========================================================

        #region Public

        public static void Initialize()
        {
            if (m_Initialized)
                return;

            string appBucket = FirebaseApp.DefaultInstance.Options.StorageBucket;
            if (!string.IsNullOrEmpty(appBucket))
                m_StorageBucket = string.Format("gs://{0}", appBucket);

            m_Initialized = true;
        }

        public static void CancelOperation()
        {
            if ((m_IsUploading || m_IsDownloading || m_IsDeleting) && m_CancellationTokenSource != null)
            {
                try
                {
                    Debug.Log("*** Cancelling operation ***");
                    m_CancellationTokenSource.Cancel();
                    m_CancellationTokenSource = null;
                }
                catch (Exception ex)
                {
                    Debug.Log(ex.ToString());
                }
            }
        }

        //== METADATA

        /// <summary>
        /// Download and display Metadata for the storage reference.
        /// </summary>
        private static IEnumerator IEGetMetadata(SavedFileDefinition pStoDef)
        {
            var storageReference = GetStorageReference(pStoDef);
            Debug.Log(string.Format("Bucket: {0}", storageReference.Bucket));
            Debug.Log(string.Format("Path: {0}", storageReference.Path));
            Debug.Log(string.Format("Name: {0}", storageReference.Name));
            Debug.Log(string.Format("Parent Path: {0}", storageReference.Parent != null ? storageReference.Parent.Path : "(root)"));
            Debug.Log(string.Format("Root Path: {0}", storageReference.Root.Path));
            Debug.Log(string.Format("App: {0}", storageReference.Storage.App.Name));
            var task = storageReference.GetMetadataAsync();
            yield return new WaitForTaskStorage(task);
            if (!(task.IsFaulted || task.IsCanceled))
                Debug.Log(MetadataToString(task.Result, false) + "\n");
        }

        //== DELETE

        public static void Delete(Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(false);
                return;
            }

            var storageReference = GetStorageReference(pStoDef);
            var task = storageReference.DeleteAsync();
            Debug.Log(string.Format("Deleting {0}...", storageReference.Path));

            m_IsDeleting = true;
            WaitUtil.WaitTask(task, () =>
            {
                m_IsDeleting = false;

                if (task.IsFaulted)
                    Debug.LogError(task.Exception.ToString());

                bool success = !task.IsFaulted && !task.IsCanceled;
                if (success)
                    Debug.Log(string.Format("{0} deleted", storageReference.Path));

                if (pOnFinished != null)
                    pOnFinished(success);
            });
        }

        public static void DeleteWithCoroutine(Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(false);
                return;
            }

            CoroutineUtil.StartCoroutine(IEDelete(pOnFinished, pStoDef));
        }

        private static IEnumerator IEDelete(Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            var storageReference = GetStorageReference(pStoDef);
            var task = storageReference.DeleteAsync();
            Debug.Log(string.Format("Deleting {0}...", storageReference.Path));

            m_IsDeleting = false;
            yield return new WaitForTaskStorage(task);
            m_IsDeleting = false;

            if (task.IsFaulted)
                Debug.LogError(task.Exception.ToString());

            bool success = !task.IsFaulted && !task.IsCanceled;
            if (success)
                Debug.Log(string.Format("{0} deleted", storageReference.Path));

            if (pOnFinished != null)
                pOnFinished(success);
        }

        //== DOWNLOAD / UPLOAD STREAM

        public static void UploadStream(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(false);
                return;
            }

            var storageReference = GetStorageReference(pStoDef);
            var task = storageReference.PutStreamAsync(
                new MemoryStream(Encoding.ASCII.GetBytes(pContent)),
                StringToMetadataChange(pStoDef.metaData),
                new StorageProgress<UploadState>(DisplayUploadState),
                m_CancellationTokenSource.Token, null);
            Debug.Log(string.Format("Uploading to {0} using stream...", storageReference.Path));

            m_IsUploading = true;
            WaitUtil.WaitTask(task, () =>
            {
                m_IsUploading = false;

                if (!task.IsFaulted && !task.IsCanceled)
                    Debug.Log("[storage]: Finished uploading " + MetadataToString(task.Result, false));
                else
                    Debug.LogError(string.Format("[storage]: Uploading Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

                if (task.IsFaulted)
                    Debug.LogError(task.Exception.ToString());

                if (pOnFinished != null)
                    pOnFinished(!task.IsFaulted && !task.IsCanceled);
            });
        }

        public static void UploadStreamWithCoroutine(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(false);
                return;
            }

            CoroutineUtil.StartCoroutine(IEUploadStream(pContent, pOnFinished, pStoDef));
        }

        private static IEnumerator IEUploadStream(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            var storageReference = GetStorageReference(pStoDef);
            var task = storageReference.PutStreamAsync(
                new MemoryStream(Encoding.ASCII.GetBytes(pContent)),
                StringToMetadataChange(pStoDef.metaData),
                new StorageProgress<UploadState>(DisplayUploadState),
                m_CancellationTokenSource.Token, null);
            Debug.Log(string.Format("Uploading to {0} using stream...", storageReference.Path));

            m_IsUploading = true;
            yield return new WaitForTaskStorage(task);
            m_IsUploading = false;

            if (!task.IsFaulted && !task.IsCanceled)
                Debug.Log("[storage]: Finished uploading " + MetadataToString(task.Result, false));
            else
                Debug.LogError(string.Format("[storage]: Uploading Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

            if (task.IsFaulted)
                Debug.LogError(task.Exception.ToString());

            if (pOnFinished != null)
                pOnFinished(!task.IsFaulted && !task.IsCanceled);
        }

        public static void DownloadStream(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(null, "");
                return;
            }

            string content = "";
            var storageReference = GetStorageReference(pStoDef);
            Debug.Log(string.Format("Downloading {0} with stream ...", storageReference.Path));

            var task = storageReference.GetStreamAsync((stream) =>
            {
                var buffer = new byte[1024];
                int read;
                // Read data to render in the text view.
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    content += Encoding.Default.GetString(buffer, 0, read);
                }
            }, new StorageProgress<DownloadState>(DisplayDownloadState), m_CancellationTokenSource.Token);

            m_IsDownloading = true;
            WaitUtil.WaitTask(task, () =>
            {
                m_IsDownloading = false;

                if (task.IsFaulted)
                    Debug.LogError(task.Exception.ToString());

                bool success = !task.IsFaulted && !task.IsCanceled;
                if (success)
                    Debug.Log("Finished downloading stream\n");
                else
                    Debug.LogError(string.Format("[storage]: Downloaded Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

                if (pOnFinished != null)
                    pOnFinished(task, content);
            });
        }

        public static void DownloadStreamWithCoroutine(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(null, "");
                return;
            }

            CoroutineUtil.StartCoroutine(IEDownloadStream(pOnFinished, pStoDef));
        }

        private static IEnumerator IEDownloadStream(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef)
        {
            string content = "";
            var storageReference = GetStorageReference(pStoDef);
            Debug.Log(string.Format("Downloading {0} with stream ...", storageReference.Path));

            var task = storageReference.GetStreamAsync((stream) =>
            {
                var buffer = new byte[1024];
                int read;
                // Read data to render in the text view.
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    content += Encoding.Default.GetString(buffer, 0, read);
                }
            }, new StorageProgress<DownloadState>(DisplayDownloadState), m_CancellationTokenSource.Token);

            m_IsDownloading = true;
            yield return new WaitForTaskStorage(task);
            m_IsDownloading = false;

            if (task.IsFaulted)
                Debug.LogError(task.Exception.ToString());

            bool success = !task.IsFaulted && !task.IsCanceled;
            if (success)
                Debug.Log("Finished downloading stream\n");
            else
                Debug.LogError(string.Format("[storage]: Downloaded Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

            if (pOnFinished != null)
                pOnFinished(task, content);
        }

        //== DOWNLOAD / UPLOAD FILE

        public static void UploadFromFile(Action<bool> pOnFinished, string pOriginalFilePath, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                pOnFinished.Raise(false);
                return;
            }

            var localFilenameUriString = PathToPersistentDataPathUriString(pOriginalFilePath);
            var storageReference = GetStorageReference(pStoDef);
            var task = storageReference.PutFileAsync(
                localFilenameUriString, StringToMetadataChange(pStoDef.metaData),
                new StorageProgress<UploadState>(DisplayUploadState),
                m_CancellationTokenSource.Token, null);
            Debug.Log(string.Format("Uploading '{0}' to '{1}'...", localFilenameUriString, storageReference.Path));

            m_IsUploading = true;
            WaitUtil.WaitTask(task, () =>
            {
                m_IsUploading = false;

                bool success = !task.IsFaulted && !task.IsCanceled;

                if (success)
                    Debug.Log("[storage]: Finished uploading " + MetadataToString(task.Result, false));
                else
                    Debug.LogError(string.Format("[storage]: Uploading Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

                if (task.IsFaulted)
                    Debug.LogError(task.Exception.ToString());

                if (pOnFinished != null)
                    pOnFinished(success);
            });
        }

        public static void UploadFromFileWithCoroutine(Action<bool> pOnFinished, string pOriginalFilePath, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(false);
                return;
            }

            CoroutineUtil.StartCoroutine(IEUploadFromFile(pOnFinished, pOriginalFilePath, pStoDef));
        }

        private static IEnumerator IEUploadFromFile(Action<bool> pOnFinished, string pOriginalFilePath, SavedFileDefinition pStoDef)
        {
            var localFilenameUriString = PathToPersistentDataPathUriString(pOriginalFilePath);
            var storageReference = GetStorageReference(pStoDef);
            var task = storageReference.PutFileAsync(
                localFilenameUriString, StringToMetadataChange(pStoDef.metaData),
                new StorageProgress<UploadState>(DisplayUploadState),
                m_CancellationTokenSource.Token, null);
            Debug.Log(string.Format("Uploading '{0}' to '{1}'...", localFilenameUriString, storageReference.Path));

            m_IsUploading = true;
            yield return new WaitForTaskStorage(task);
            m_IsUploading = false;

            if (!task.IsFaulted && !task.IsCanceled)
                Debug.Log("[storage]: Finished uploading " + MetadataToString(task.Result, false));
            else
                Debug.LogError(string.Format("[storage]: Uploading Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

            if (task.IsFaulted)
                Debug.LogError(task.Exception.ToString());

            if (pOnFinished != null)
                pOnFinished(!task.IsFaulted && !task.IsCanceled);
        }

        public static void DownloadToFile(Action<Task, string> pOnFinished, string pOutPutPath, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(null, "");
                return;
            }

            var storageReference = GetStorageReference(pStoDef);
            var localFilenameUriString = PathToPersistentDataPathUriString(pOutPutPath);
            var task = storageReference.GetFileAsync(localFilenameUriString, new StorageProgress<DownloadState>(DisplayDownloadState), m_CancellationTokenSource.Token);
            var content = "";
            Debug.Log(string.Format("Downloading {0} to {1}...", storageReference.Path, localFilenameUriString));

            m_IsDownloading = true;
            WaitUtil.WaitTask(task, () =>
            {
                m_IsDownloading = false;

                if (task.IsFaulted)
                    Debug.LogError(task.Exception.ToString());

                bool success = !task.IsFaulted && !task.IsCanceled;
                if (success)
                {
                    var filename = FileUriStringToPath(localFilenameUriString);
                    Debug.Log(string.Format("Finished downloading file {0} ({1})", localFilenameUriString, filename));
                    Debug.Log(string.Format("File Size {0} bytes\n", (new FileInfo(filename)).Length));
                    content = File.ReadAllText(filename);
                }
                else
                    Debug.LogError(string.Format("[storage]: Downloaded Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

                if (pOnFinished != null)
                    pOnFinished(task, content);
            });
        }

        public static void DownloadToFileWithCoroutine(Action<Task, string> pOnFinished, string pOutPutPath, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(null, "");
                return;
            }

            CoroutineUtil.StartCoroutine(IEDownloadToFile(pOnFinished, pOutPutPath, pStoDef));
        }

        private static IEnumerator IEDownloadToFile(Action<Task, string> pOnFinished, string pOutPutPath, SavedFileDefinition pStoDef)
        {
            var storageReference = GetStorageReference(pStoDef);
            var localFilenameUriString = PathToPersistentDataPathUriString(pOutPutPath);
            var task = storageReference.GetFileAsync(localFilenameUriString, new StorageProgress<DownloadState>(DisplayDownloadState), m_CancellationTokenSource.Token);
            var content = "";
            Debug.Log(string.Format("Downloading {0} to {1}...", storageReference.Path, localFilenameUriString));

            m_IsDownloading = true;
            yield return new WaitForTaskStorage(task);
            m_IsDownloading = false;

            if (task.IsFaulted)
                Debug.LogError(task.Exception.ToString());

            bool success = !task.IsFaulted && !task.IsCanceled;
            if (success)
            {
                var filename = FileUriStringToPath(localFilenameUriString);
                Debug.Log(string.Format("Finished downloading file {0} ({1})", localFilenameUriString, filename));
                Debug.Log(string.Format("File Size {0} bytes\n", (new FileInfo(filename)).Length));
                content = File.ReadAllText(filename);
            }
            else
                Debug.LogError(string.Format("[storage]: Downloaded Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

            if (pOnFinished != null)
                pOnFinished(task, content);
        }

        //== DOWNLOAD / UPLOAD BYTES

        public static void DownloadFile(SavedFileDefinition pStoDef, Action<string> pFoundFile, Action pNotFoundFile, Action pFailed)
        {
            if (!m_Initialized)
                pFailed.Raise();

            DownloadBytes((task, content) =>
            {
                bool success = task != null && !task.IsFaulted && !task.IsCanceled;
                if (success)
                    pFoundFile.Raise(content);
                else if (task != null)
                {
                    if (task.IsFaulted)
                    {
                        string exception = task.Exception.ToString().ToLower();
                        if (exception.Contains("not found") || exception.Contains("not exist"))
                            pNotFoundFile.Raise();
                        else
                            pFailed.Raise();
                    }
                    else
                        pFailed.Raise();
                }
                else
                    pFailed.Raise();
            }, pStoDef);
        }
        public static void UploadBytesWithCoroutine(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(false);
                return;
            }

            CoroutineUtil.StartCoroutine(IEUploadBytes(pContent, pOnFinished, pStoDef));
        }

        /// <summary>
        /// Remember coroutine can break when scene changed
        /// </summary>
        private static IEnumerator IEUploadBytes(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            var storageReference = GetStorageReference(pStoDef);
            var task = storageReference.PutBytesAsync(Encoding.UTF8.GetBytes(pContent), StringToMetadataChange(pStoDef.metaData), new StorageProgress<UploadState>(DisplayUploadState), m_CancellationTokenSource.Token, null);
            Debug.Log(string.Format("Uploading to {0} ...", storageReference.Path));

            m_IsUploading = true;
            yield return new WaitForTaskStorage(task);
            m_IsUploading = false;

            if (!task.IsFaulted && !task.IsCanceled)
                Debug.Log("[storage]: Finished uploading " + MetadataToString(task.Result, false));
            else
                Debug.LogError(string.Format("[storage]: Uploading Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

            if (task.IsFaulted)
                Debug.LogError(task.Exception.ToString());

            if (pOnFinished != null)
                pOnFinished(!task.IsFaulted && !task.IsCanceled);
        }

        public static void UploadBytes(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(false);
                return;
            }

            var storageReference = GetStorageReference(pStoDef);
            var task = storageReference.PutBytesAsync(Encoding.UTF8.GetBytes(pContent), StringToMetadataChange(pStoDef.metaData), new StorageProgress<UploadState>(DisplayUploadState), m_CancellationTokenSource.Token, null);
            Debug.Log(string.Format("Uploading to {0} ...", storageReference.Path));

            m_IsUploading = true;
            WaitUtil.WaitTask(task, () =>
            {
                m_IsUploading = false;

                bool success = !task.IsFaulted && !task.IsCanceled;
                if (success)
                    Debug.Log("[storage]: Finished uploading " + MetadataToString(task.Result, false));
                else
                    Debug.LogError(string.Format("[storage]: Uploading Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

                if (task.IsFaulted)
                    Debug.LogError(task.Exception.ToString());

                if (pOnFinished != null)
                    pOnFinished(success);
            });
        }

        public static void DownloadBytes(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(null, "");
                return;
            }

            var storageRef = GetStorageReference(pStoDef);
            var task = storageRef.GetBytesAsync(0, new StorageProgress<DownloadState>(DisplayDownloadState), m_CancellationTokenSource.Token);
            var content = "";
            Debug.Log(string.Format("[storage]: Downloading {0} ...", storageRef.Path));

            m_IsDownloading = true;
            WaitUtil.WaitTask(task, () =>
            {
                m_IsDownloading = false;

                bool success = !task.IsFaulted && !task.IsCanceled;
                if (success)
                {
                    content = Encoding.Default.GetString(task.Result);
                    Debug.Log(string.Format("[storage]: Finished downloading bytes\nFile Size {0} bytes\n", content.Length));
                }
                else
                    Debug.LogError(string.Format("[storage]: Downloaded Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

                if (task.IsFaulted)
                    LogException(task.Exception);

                if (pOnFinished != null)
                    pOnFinished(task, content);
            });
        }

        public static void DownloadBytesWithCoroutine(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (!m_Initialized || string.IsNullOrEmpty(pStoDef.fileName))
            {
                if (pOnFinished != null)
                    pOnFinished(null, "");
                return;
            }

            CoroutineUtil.StartCoroutine(IEDownLoadBytes(pOnFinished, pStoDef));
        }

        private static IEnumerator IEDownLoadBytes(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef)
        {
            var storageRef = GetStorageReference(pStoDef);
            var task = storageRef.GetBytesAsync(0, new StorageProgress<DownloadState>(DisplayDownloadState), m_CancellationTokenSource.Token);
            var content = "";
            Debug.Log(string.Format("[storage]: Downloading {0} ...", storageRef.Path));

            m_IsDownloading = true;
            yield return new WaitForTaskStorage(task);
            m_IsDownloading = false;

            if (task.IsFaulted)
                Debug.LogError(task.Exception.ToString());

            bool success = !task.IsFaulted && !task.IsCanceled;
            if (success)
            {
                content = Encoding.Default.GetString(task.Result);
                Debug.Log(string.Format("[storage]: Finished downloading bytes\nFile Size {0} bytes\n", content.Length));
            }
            else
                Debug.LogError(string.Format("[storage]: Downloaded Fail, cancel: {0}, faulted: {1}", task.IsCanceled, task.IsFaulted));

            if (pOnFinished != null)
                pOnFinished(task, content);

        }

        //==========================

        /// <summary>
        /// Get a local filesystem path from a file:// URI.
        /// </summary>
        private static string FileUriStringToPath(string fileUriString)
        {
            return Uri.UnescapeDataString((new Uri(fileUriString)).PathAndQuery);
        }

        /// <summary>
        /// Retrieve a storage reference from the user specified path.
        /// </summary>
        private static StorageReference GetStorageReference(SavedFileDefinition pStoDef)
        {
            string location = pStoDef.GetStorageLocation(m_StorageBucket);
            // If this is an absolute path including a bucket create a storage instance.
            if (location.StartsWith("gs://") || location.StartsWith("http://") || location.StartsWith("https://"))
            {
                var storageUri = new Uri(location);
                var firebaseStorage = FirebaseStorage.GetInstance(string.Format("{0}://{1}", storageUri.Scheme, storageUri.Host));
                return firebaseStorage.GetReferenceFromUrl(location);
            }

            // When using relative paths use the default storage instance which uses the bucket supplied on creation of FirebaseApp.
            return FirebaseStorage.DefaultInstance.GetReference(location);
        }

        /// <summary>
        /// Get the local filename as a URI relative to the persistent data path if the path isn't already a file URI.
        /// </summary>
        private static string PathToPersistentDataPathUriString(string filename)
        {
            if (filename.StartsWith(URI_FILE_SCHEME))
                return filename;

            return string.Format("{0}{1}/{2}", URI_FILE_SCHEME, Application.persistentDataPath, filename);
        }

        /// <summary>
        /// Write upload state to the log.
        /// </summary>
        private static void DisplayUploadState(UploadState uploadState)
        {
            if (m_IsUploading)
                Debug.Log(string.Format("Uploading {0}: {1} out of {2}", uploadState.Reference.Name, uploadState.BytesTransferred, uploadState.TotalByteCount));
        }

        /// <summary>
        /// Convert a string in the form:
        /// key1=value1
        /// ...
        /// keyN=valueN
        /// to a MetadataChange object.
        /// If an empty string is provided this method returns null.
        /// </summary>
        private static MetadataChange StringToMetadataChange(string metadataString)
        {
            var metadataChange = new MetadataChange();
            var customMetadata = new Dictionary<string, string>();
            bool hasMetadata = false;
            foreach (var metadataStringLine in metadataString.Split(new char[] { '\n' }))
            {
                if (metadataStringLine.Trim() == "")
                    continue;

                var keyValue = metadataStringLine.Split(new char[] { '=' });
                if (keyValue.Length != 2)
                {
                    Debug.Log(string.Format("Ignoring malformed metadata line '{0}' tokens={1}", metadataStringLine, keyValue.Length));
                    continue;
                }

                hasMetadata = true;

                var key = keyValue[0];
                var value = keyValue[1];
                if (key == "CacheControl")
                    metadataChange.CacheControl = value;
                else if (key == "ContentDisposition")
                    metadataChange.ContentDisposition = value;
                else if (key == "ContentEncoding")
                    metadataChange.ContentEncoding = value;
                else if (key == "ContentLanguage")
                    metadataChange.ContentLanguage = value;
                else if (key == "ContentType")
                    metadataChange.ContentType = value;
                else
                    customMetadata[key] = value;
            }
            if (customMetadata.Count > 0)
                metadataChange.CustomMetadata = customMetadata;
            return hasMetadata ? metadataChange : null;
        }

        /// <summary>
        /// Convert a Metadata object to a string.
        /// </summary>
        private static string MetadataToString(StorageMetadata metadata, bool onlyMutableFields)
        {
            var fieldsAndValues = new Dictionary<string, object> {
                {"ContentType", metadata.ContentType},
                {"CacheControl", metadata.CacheControl},
                {"ContentDisposition", metadata.ContentDisposition},
                {"ContentEncoding", metadata.ContentEncoding},
                {"ContentLanguage", metadata.ContentLanguage}
              };
            if (!onlyMutableFields)
            {
                foreach (var kv in new Dictionary<string, object> {
                            {"Reference", metadata.Reference != null ? metadata.Reference.Path : null},
                            {"Path", metadata.Path},
                            {"Name", metadata.Name},
                            {"Bucket", metadata.Bucket},
                            {"Generation", metadata.Generation},
                            {"MetadataGeneration", metadata.MetadataGeneration},
                            {"CreationTimeMillis", metadata.CreationTimeMillis},
                            {"UpdatedTimeMillis", metadata.UpdatedTimeMillis},
                            {"SizeBytes", metadata.SizeBytes},
                            {"Md5Hash", metadata.Md5Hash}
                         })
                {
                    fieldsAndValues[kv.Key] = kv.Value;
                }
            }
            foreach (var key in metadata.CustomMetadataKeys)
            {
                fieldsAndValues[key] = metadata.GetCustomMetadata(key);
            }
            var fieldAndValueStrings = new List<string>();
            foreach (var kv in fieldsAndValues)
            {
                fieldAndValueStrings.Add(string.Format("{0}={1}", kv.Key, kv.Value));
            }
            return string.Join("\n", fieldAndValueStrings.ToArray());
        }

        /// <summary>
        /// Write download state to the log.
        /// </summary>
        private static void DisplayDownloadState(DownloadState downloadState)
        {
            if (m_IsDownloading)
                Debug.Log(string.Format("Downloading {0}: {1} out of {2}", downloadState.Reference.Name, downloadState.BytesTransferred, downloadState.TotalByteCount));
        }

        private static void LogException(Exception exception)
        {
            var storageException = exception as StorageException;
            if (storageException != null)
            {
                Debug.LogError(string.Format("[storage]: Error Code: {0}", storageException.ErrorCode));
                Debug.LogError(string.Format("[storage]: HTTP Result Code: {0}", storageException.HttpResultCode));
                Debug.LogError(string.Format("[storage]: Recoverable: {0}", storageException.IsRecoverableException));
                Debug.LogError("[storage]: " + storageException.ToString());
            }
            else
            {
                Debug.LogError(exception.ToString());
            }
        }

        #endregion
#else
        public static bool Initialized { get { return false; } }
        public static void Initialize() { }
        public static void CancelOperation() { }
        public static void Delete(Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (pOnFinished != null)
                pOnFinished(false);
        }
        public static void DeleteWithCoroutine(Action<bool> pOnFinished, SavedFileDefinition pStoDef) { }
        public static void UploadStream(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (pOnFinished != null)
                pOnFinished(false);
        }
        public static void UploadStreamWithCoroutine(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef) { }
        public static void DownloadStream(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (pOnFinished != null)
                pOnFinished(Task.FromResult(0), "");
        }
        public static void DownloadStreamWithCoroutine(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef) { }
        public static void UploadFromFile(Action<bool> pOnFinished, string pOriginalFilePath, SavedFileDefinition pStoDef)
        {
            if (pOnFinished != null)
                pOnFinished(false);
        }
        public static void UploadFromFileWithCoroutine(Action<bool> pOnFinished, string pOriginalFilePath, SavedFileDefinition pStoDef) { }
        public static void DownloadToFile(Action<Task, string> pOnFinished, string pOutPutPath, SavedFileDefinition pStoDef)
        {
            if (pOnFinished != null)
                pOnFinished(Task.FromResult(0), "");
        }
        public static void DownloadToFileWithCoroutine(Action<Task, string> pOnFinished, string pOutPutPath, SavedFileDefinition pStoDef) { }
        public static void UploadBytesWithCoroutine(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef) { }
        public static void UploadBytes(string pContent, Action<bool> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (pOnFinished != null)
                pOnFinished(false);
        }
        public static void DownloadBytes(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef)
        {
            if (pOnFinished != null)
                pOnFinished(Task.FromResult(0), "");
        }
        public static void DownloadBytesWithCoroutine(Action<Task, string> pOnFinished, SavedFileDefinition pStoDef) { }
        public static void DownloadFile(SavedFileDefinition pStoDef, Action<string> pFoundFile, Action pNotFoundFile, Action pFailed)
        {
            pFailed?.Invoke();
        }
#endif
    }
}