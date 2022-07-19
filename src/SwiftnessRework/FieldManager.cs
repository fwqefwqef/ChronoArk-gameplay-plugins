﻿using BepInEx;
using BepInEx.Configuration;
using GameDataEditor;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using System.Linq;
using Debug = UnityEngine.Debug;
using System;

namespace SwiftnessRework
{

    abstract public class FieldManager<Tkey, TfieldVal>
    {

        public struct RefHolder
        {
            public WeakReference weakRef;
            public TfieldVal value;
        }

        public Dictionary<Tkey, List<RefHolder>> fieldDict;

        // any custom method depending to reduce "key collision"
        // some value which differentiates the instances in interest but doesn't change through lifetime of the instance
        // example could be instance.GetType().Name if 
        // default inst.GetHashCode() could still be good enough in terms of functionality
        public abstract Tkey ComputeKey(object inst);

        public FieldManager()
        {
            fieldDict = new Dictionary<Tkey, List<RefHolder>>();
        }

        public FieldManager(int cap)
        {
            fieldDict = new Dictionary<Tkey, List<RefHolder>>(cap);
        }


        public void AddField(object inst, TfieldVal val)
        {
            var dict = fieldDict;
            var key = ComputeKey(inst);
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, new List<RefHolder>() { new RefHolder() { weakRef = new WeakReference(inst, false), value = val } });
            }
            else
            {
                Debug.Log($"its already in!!! {key} hashcode (object: {inst})");
                dict[key].Add(new RefHolder() { weakRef = new WeakReference(inst, false), value = val });
            }

        }

        bool LoopBucket(object inst, List<RefHolder> bucket, out TfieldVal result, out int index)
        {
            int eCount = 0;
            var found = false;

            result = default(TfieldVal);
            index = -1;
            var i = 0;
            if (bucket.Count == 1)
            {
                result = bucket[0].value;
                index = 0;
                return true;
            }

            foreach (var refholder in bucket.ToArray())
            {
                if (!refholder.weakRef.IsAlive)
                {
                    bucket.Remove(refholder);
                    continue;
                }
                if(refholder.weakRef.Target == null)
                    Debug.Log($"target is null!");
                // can target become null right before equality is tested?
                if (inst.Equals(refholder.weakRef.Target))
                {
                    eCount++;
                    result = refholder.value;
                    found = true;
                    index = i;
                }
                i++;
                
            }
            if (eCount > 1)
            {
                Debug.Log($"equals is shit {ComputeKey(inst)} hashcode (object: {inst})");
            }

            return found;
            
        }

        public TfieldVal GetVal(object inst)
        {
            var key = ComputeKey(inst);
            if (fieldDict.TryGetValue(key, out List<RefHolder> bucket))
            {
                if (LoopBucket(inst, bucket, out TfieldVal outValue, out int i))
                {
                    return outValue;
                }
                else
                {
                    Debug.Log($"GetVal: no ENTRY with {key} hashcode (object: {inst})");
                    throw new Exception($"GetVal: no ENTRY with {key} hashcode (object: {inst})");
                }
            }
            else
            {

                Debug.Log($"GetVal: no BUCKET with {key} hashcode (object: {inst})");
                throw new Exception($"GetVal: no BUCKET with {ComputeKey(inst)} hashcode (object: {inst})");
            }
            

        }

        public void SetVal(object inst, TfieldVal val)
        {
            var key = ComputeKey(inst);
            if (fieldDict.TryGetValue(key, out List<RefHolder> bucket))
            {

                if (LoopBucket(inst, bucket, out TfieldVal outValue, out int i))
                {
                    bucket[i] = new RefHolder() { weakRef = bucket[0].weakRef, value = val };
                    fieldDict[key] = bucket;
                }
                else
                {
                    Debug.Log($"Setval: no ENTRY with {key} hashcode (object: {inst})");
                    throw new Exception($"Setval: no ENTRY with {key} hashcode (object: {inst})");
                }

            }
            else
            {
                Debug.Log($"SetVal: no BUCKET with {ComputeKey(inst)} hashcode (object: {inst})");
                throw new Exception($"SetVal: no BUCKET with {ComputeKey(inst)} hashcode (object: {inst})");
            }
        }



        public void CullDestroyed()
        {
            Debug.Log("fieldDict size before: " + fieldDict.Count);
            var keys2Remove = new List<Tkey>();
            foreach (var kv in fieldDict)
            {
                foreach (var bucketEntry in kv.Value.ToArray())
                {
                    if (!bucketEntry.weakRef.IsAlive)
                    {
                        fieldDict[kv.Key].Remove(bucketEntry);
                    }
                }
                if (fieldDict[kv.Key].Count == 0)
                {
                    keys2Remove.Add(kv.Key);
                }

            }
            foreach (var k in keys2Remove)
            {
                fieldDict.Remove(k);
            }

            Debug.Log("fieldDict size after: " + fieldDict.Count);
        }



    }
}