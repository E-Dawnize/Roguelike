using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Core.DI
{
    // 可选：缺失依赖时跳过
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectOptionalAttribute : Attribute {}

    public partial class DIContainer
    {
        private static readonly BindingFlags InjectFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly ConcurrentDictionary<Type, InjectMember[]> _injectMembersCache = new();//需要注入的字段缓存

        /// <summary>
        /// 注入对象info（非实例字段引用）
        /// </summary>
        private readonly struct InjectMember
        {
            public readonly Type MemberType;
            public readonly string Name;
            public readonly bool Optional;
            public readonly Action<object, object> Setter;

            public InjectMember(Type memberType, string name, bool optional, Action<object, object> setter)
            {
                MemberType = memberType;
                Name = name;
                Optional = optional;
                Setter = setter;
            }
        }

        public void Inject(object target, IScope scope = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            // Unity 对象被销毁时会 == null
            if (target is UnityEngine.Object uo && uo == null) return;

            var provider = scope?.ServiceProvider ?? (IServiceProvider)this;//依托作用域
            var members = _injectMembersCache.GetOrAdd(target.GetType(), BuildInjectMembers);//得到对象对应需要注入的字段

            foreach (var m in members)
            {
                var dep = provider.GetService(m.MemberType);
                if (dep == null)
                {
                    if (m.Optional) continue;
                    throw new InvalidOperationException(
                        $"Missing dependency: {m.MemberType} for {target.GetType().Name}.{m.Name}");
                }
                m.Setter(target, dep);//注入，m为字段info，实际注入引用根据传参target得到
            }
        }
        /// <summary>
        /// 获取类中需要注入的对象列表
        /// </summary>
        /// <param name="type"></param>
        /// <returns>注入对象info列表</returns>
        private static InjectMember[] BuildInjectMembers(Type type)
        {
            var list = new List<InjectMember>();

            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var f in t.GetFields(InjectFlags))
                {
                    if (!f.IsDefined(typeof(InjectAttribute), true)) continue;
                    if (f.IsInitOnly) continue; // 跳过 readonly
                    var optional = f.IsDefined(typeof(InjectOptionalAttribute), true);
                    list.Add(new InjectMember(
                        f.FieldType,
                        f.Name,
                        optional,
                        (obj, val) => f.SetValue(obj, val)));
                }

                foreach (var p in t.GetProperties(InjectFlags))
                {
                    if (!p.IsDefined(typeof(InjectAttribute), true)) continue;
                    if (!p.CanWrite) continue;
                    if (p.GetIndexParameters().Length != 0) continue;
                    var set = p.GetSetMethod(true);
                    if (set == null) continue;

                    var optional = p.IsDefined(typeof(InjectOptionalAttribute), true);
                    list.Add(new InjectMember(
                        p.PropertyType,
                        p.Name,
                        optional,
                        (obj, val) => p.SetValue(obj, val)));
                }
            }

            return list.ToArray();
        }
    }
}