using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ConstrainedExecution;
using UnityEngine;
using UnityEngine.Scripting;

namespace LiveKit
{
    public class JSRef : CriticalFinalizerObject
    {
        private static readonly Dictionary<string, Type> s_TypeMap = new Dictionary<string, Type>()
        {
            {"Number", typeof(JSNumber)},
            {"String", typeof(JSString)},
            {"Boolean", typeof(JSBoolean)},
            {"Error", typeof(JSError)},
            {"LivekitError", typeof(LivekitError)},
            {"ConnectionError", typeof(ConnectionError)},
            {"TrackInvalidError", typeof(TrackInvalidError)},
            {"UnsupportedServer", typeof(UnsupportedServer)},
            {"UnexpectedConnectionState", typeof(UnexpectedConnectionState)},
            {"PublishDataError", typeof(PublishDataError)},
            {"Room", typeof(Room)},
            {"Participant", typeof(Participant)},
            {"LocalParticipant", typeof(LocalParticipant)},
            {"RemoteParticipant", typeof(RemoteParticipant)},
            {"Track", typeof(Track)},
            {"RemoteTrack", typeof(RemoteTrack)},
            {"RemoteVideoTrack", typeof(RemoteVideoTrack)},
            {"RemoteAudioTrack", typeof(RemoteAudioTrack)},
            {"LocalTrack", typeof(LocalTrack)},
            {"LocalVideoTrack", typeof(LocalVideoTrack)},
            {"LocalAudioTrack", typeof(LocalAudioTrack)},
            {"TrackPublication", typeof(TrackPublication)},
            {"RemoteTrackPublication", typeof(RemoteTrackPublication)},
            {"LocalTrackPublication", typeof(LocalTrackPublication)},
            {"HTMLVideoElement", typeof(HTMLVideoElement)},
            {"HTMLAudioElement", typeof(HTMLAudioElement)},
        };

        private static readonly Dictionary<IntPtr, WeakReference<JSRef>> Cache = new Dictionary<IntPtr, WeakReference<JSRef>>();
        private static readonly HashSet<object> AliveCache = new HashSet<object>(); // Used to hold a reference and release it manually

        internal JSHandle NativePtr { get; } // Own the handle

        internal static T Acquire<T>(JSHandle handle) where T : JSRef
        {
            if (handle.IsClosed || handle.IsInvalid)
                throw new Exception("Trying to acquire an invalid handle");

            var ptr = handle.DangerousGetHandle();
            if (Cache.TryGetValue(ptr, out var wRef) && wRef.TryGetTarget(out JSRef jsRef))
                return jsRef as T;

            var type = typeof(T);
            if (JSNative.IsObject(handle))
            {
                // Maintain class hierarchy 
                JSNative.PushString("constructor");
                var ctor = JSNative.GetProperty(handle);

                JSNative.PushString("name");
                var typeName = Acquire<JSString>(JSNative.GetProperty(ctor));

                if (s_TypeMap.TryGetValue(typeName.ToString(), out Type correctType))
                    type = correctType;
            }

            return Activator.CreateInstance(type, handle) as T;
        }

        internal static T AcquireOrNull<T>(JSHandle ptr) where T : JSRef
        {
            if (JSNative.IsUndefined(ptr) || JSNative.IsNull(ptr))
                return null;

            return Acquire<T>(ptr);
        }

        internal static JSRef AcquireOrNull(JSHandle ptr)
        {
            return AcquireOrNull<JSRef>(ptr);
        }

        internal static void SetKeepAlive([NotNull] object reff, bool keepAlive)
        {
            if (reff == null)
                throw new ArgumentNullException(nameof(reff));
            
            Log.Info($"SetKeepAlive of {reff} to {keepAlive}");
            
            if (keepAlive)
                AliveCache.Add(reff);
            else
                AliveCache.Remove(reff);
        }

        [Preserve]
        public JSRef(JSHandle ptr)
        {
            NativePtr = ptr;

            // We add the instantiated object into the cache se we can retrieve it later if not garbage collected.
            // Note that if JSRef has been garbage collected, it doesn't mean that the key doesn't exists on this map ( Finalizer is unpredictable )
            Cache[ptr.DangerousGetHandle()] = new WeakReference<JSRef>(this);
        }

        internal JSRef() : this(JSNative.NewRef())
        {
            
        }

        ~JSRef()
        {
            var ptr = NativePtr.DangerousGetHandle();
            if (Cache[ptr].TryGetTarget(out var _))
            {
                // It means that another instance has been created after this one being GC
                // il2cpp doesn't support Long WeakReference
                return;
            }

            Cache.Remove(ptr);
        }
    }
}