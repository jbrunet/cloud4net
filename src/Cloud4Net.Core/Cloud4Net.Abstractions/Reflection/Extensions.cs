#region License
// Copyright (c) 2009-2010 Topian System - http://www.topian.net
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Reflection;

namespace System.StorageModel.Reflection
{
    public static class SystemExtensions
    {
        public static bool As<T>(this object obj, out T cast)
            where T : class
        {
            cast = obj as T;
            return (cast != null);
        }
    }

    public static class ReflectionExtensions
    {
        #region Attributes

        public static bool TryGetCustomAttribute<T>(this Assembly assembly, bool inherit, out T attribute)
            where T : Attribute
        {
            return Attribute.GetCustomAttribute(assembly, typeof(T), inherit).As(out attribute);
        }

        public static bool TryGetCustomAttribute<T>(this MemberInfo member, bool inherit, out T attribute)
            where T : Attribute
        {
            return Attribute.GetCustomAttribute(member, typeof(T), inherit).As(out attribute);
        }

        public static bool TryGetCustomAttribute<T>(this ParameterInfo parameter, bool inherit, out T attribute)
            where T : Attribute
        {
            return Attribute.GetCustomAttribute(parameter, typeof(T), inherit).As(out attribute);
        }

        public static bool TryGetCustomAttribute<T>(this Module module, bool inherit, out T attribute)
            where T : Attribute
        {
            return Attribute.GetCustomAttribute(module, typeof(T), inherit).As(out attribute);
        }

        #endregion

        #region Delegates

        private static Delegate CreateDelegate<DelegateType, TResult>(object target, string methodName, params Type[] types)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance, null,
                                                    types,
                                                    null);
            if (method == null)
                throw new NotSupportedException(string.Format("Method '{0}' not found", methodName));
            if (!typeof(TResult).IsAssignableFrom(method.ReturnType))
                throw new NotSupportedException(string.Format("Method '{0}' returns '{1}', not '{2}", methodName,
                                                              method.ReturnType, typeof(TResult)));
            return Delegate.CreateDelegate(typeof(DelegateType), target, method, true);
        }

        public static Func<TResult> GetMethod<TResult>(this object obj, string methodName)
        {
            return (Func<TResult>)CreateDelegate<Func<TResult>, TResult>(obj, methodName);
        }

        public static Func<T, TResult> GetMethod<T, TResult>(this object obj, string methodName)
        {
            return (Func<T, TResult>)CreateDelegate<Func<T, TResult>, TResult>(obj, methodName, typeof(T));
        }

        public static Func<T1, T2, TResult> GetMethod<T1, T2, TResult>(this object obj, string methodName)
        {
            return (Func<T1, T2, TResult>)CreateDelegate<Func<T1, T2, TResult>, TResult>(obj, methodName, typeof(T1), typeof(T2));
        }

        #endregion

        private static FieldInfo GetField(Type type, string fieldName, bool isStatic)
        {
            var field = type.GetField(fieldName
                                      , (isStatic ? BindingFlags.Static : BindingFlags.Instance)
                                        | BindingFlags.Public | BindingFlags.NonPublic
                );
            if (field == null)
            {
                if (type.BaseType == typeof(object))
                    throw new NotSupportedException((isStatic ? "Static" : "Instance") + " Field " + fieldName +
                                                    " does not exist in type " +
                                                    type.FullName);
                return GetField(type.BaseType, fieldName, isStatic);
            }
            return field;
        }

        public static T GetInstanceField<T>(this object obj, string fieldName)
        {
            return (T)GetField(obj.GetType(), fieldName, false).GetValue(obj);
        }

        public static void SetInstanceField<T>(this object obj, string fieldName, T value)
        {
            var field = GetField(obj.GetType(), fieldName, false);
            field.SetValue(obj, value);
        }

        public static T GetStaticField<T>(this Type type, string fieldName)
        {
            return (T)GetField(type, fieldName, true).GetValue(null);
        }

        public static PropertyInfo GetProperty(Type type, string propertyName, bool isStatic)
        {
            var prop = type.GetProperty(propertyName
                , (isStatic ? BindingFlags.Static : BindingFlags.Instance)
                | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null)
                throw new NotSupportedException("Static Property " + propertyName + " does not exist in type " +
                                                type.FullName);
            return prop;
        }

        public static T GetStaticProperty<T>(this Type type, string propertyName)
        {
            return (T)GetProperty(type, propertyName, true).GetValue(null, null);
        }

        public static T GetInstanceProperty<T>(this object obj, string propertyName)
        {
            return (T)GetProperty(obj.GetType(), propertyName, false).GetValue(obj, null);
        }

        //private static MethodInfo GetMethod(Type type, string methodName, params Type[] types)
        //{
        //    var method = type.GetMethod(methodName
        //                                , BindingFlags.Instance
        //                                  | BindingFlags.Public | BindingFlags.NonPublic
        //                                , null, types, null);
        //    if (method == null)
        //        throw new NotSupportedException("Method " + methodName + " does not exist in type " + type.FullName);
        //    return method;
        //}

        //public static T Invoke<T>(this object obj, string methodName)
        //{
        //    return (T)GetMethod(obj.GetType(), methodName).Invoke(obj, null);
        //}

        //public static T Invoke<T1, T>(this object obj, string methodName, T1 arg1)
        //{
        //    return (T)GetMethod(obj.GetType(), methodName, arg1.GetType()).Invoke(obj, new object[] { arg1 });
        //}
    }
}