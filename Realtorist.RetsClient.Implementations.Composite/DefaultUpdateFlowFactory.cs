using System;
using System.Collections.Generic;
using Realtorist.Models.Listings.Enums;
using Realtorist.RetsClient.Abstractions;
using Realtorist.RetsClient.Implementations.Crea.Implementations;

namespace Realtorist.RetsClient.Implementations.Composite
{
    /// <summary>
    /// Default implementation for the update flow factory
    /// </summary>
    public class DefaultUpdateFlowFactory : IUpdateFlowFactory
    {
        private readonly static IReadOnlyDictionary<ListingSource, Type> _flows = new Dictionary<ListingSource, Type>
        {
            { ListingSource.Crea, typeof(CreaUpdateFlow) }
        };
        
        public Type GetUpdateFlowType(ListingSource listingSource)
        {
            if (!_flows.ContainsKey(listingSource))
            {
                throw new ArgumentException($"Unknown listing source: {listingSource}");
            }

            return _flows[listingSource];
        }
    }
}