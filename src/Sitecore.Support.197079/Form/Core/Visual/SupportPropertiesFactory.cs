using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.WFFM.Abstractions.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.Form.Core.Visual;
using Sitecore.WFFM.Abstractions.Dependencies;
using Sitecore.Collections;

namespace Sitecore.Support.Form.Core.Visual
{
    public class SupportPropertiesFactory : PropertiesFactory
    {
        public SupportPropertiesFactory(Item item, IItemRepository itemRepository, IResourceManager resourceManager)
          : base(item, itemRepository, resourceManager)
        {
        }

        internal static IEnumerable<string> SupportCompareTypes(
          IEnumerable<Pair<string, string>> properties, Item newType, Item oldType, ID assemblyField, ID classField)
        {
            var enumerable = properties as Pair<string, string>[] ?? properties.ToArray();
            if (properties != null && enumerable.Any())
            {
                List<VisualPropertyInfo> newTypeInfos = new SupportPropertiesFactory(newType, DependenciesManager.Resolve<IItemRepository>(), DependenciesManager.Resolve<IResourceManager>()).GetProperties(assemblyField, classField);
                List<VisualPropertyInfo> oldTypeInfos = new SupportPropertiesFactory(oldType, DependenciesManager.Resolve<IItemRepository>(), DependenciesManager.Resolve<IResourceManager>()).GetProperties(assemblyField, classField);

                IEnumerable<string> fill = new string[] { };
                if (oldTypeInfos.Count > 0)
                {
                    fill = from p in enumerable
                           where oldTypeInfos.FirstOrDefault(s => s.PropertyName.ToLower() == p.Part1.ToLower()) != null &&
                                 oldTypeInfos.FirstOrDefault(s => s.PropertyName.ToLower() == p.Part1.ToLower()).DefaultValue.ToLower() != p.Part2.ToLower()
                           select p.Part1.ToLower();
                }

                return from f in fill
                       where newTypeInfos.Find(s => s.PropertyName.ToLower() == f) == default(VisualPropertyInfo)
                       select oldTypeInfos.Find(s => s.PropertyName.ToLower() == f).DisplayName.TrimEnd(new[] { ' ', ':' });
            }

            return new string[] { };
        }
    }
}
