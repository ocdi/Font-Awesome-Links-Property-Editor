using FaLinksPropertyEditor.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;
using static Umbraco.Cms.Core.Constants;

namespace FaLinksPropertyEditor.PropertyValueConverters
{
    class FaLinksPropertyValueConverter : PropertyValueConverterBase
    {

        private readonly IPublishedSnapshotAccessor _publishedSnapshotAccessor;


        public FaLinksPropertyValueConverter(IPublishedSnapshotAccessor publishedSnapshotAccessor)
        {
            _publishedSnapshotAccessor = publishedSnapshotAccessor ?? throw new ArgumentNullException(nameof(publishedSnapshotAccessor));
        }
        
        /// <summary>
        /// Gets a value indicating whether the converter supports a property type.
        /// </summary>
        /// <param name="propertyType">The property type.</param>
        /// <returns>A value indicating whether the converter supports a property type.</returns>
        public override bool IsConverter(IPublishedPropertyType propertyType) =>
            (propertyType.EditorAlias).Equals("FaLinksPropertyEditor");


        /// <summary>
        /// Gets the type of values returned by the converter.
        /// </summary>
        /// <param name="propertyType">The property type.</param>
        /// <returns>The CLR type of values returned by the converter.</returns>
        public override Type GetPropertyValueType(IPublishedPropertyType propertyType)
            => GetMaxNumber(propertyType.DataType.Configuration) == 1 ?
                typeof(FaLink) :
                typeof(IEnumerable<FaLink>);

        public override PropertyCacheLevel GetPropertyCacheLevel(IPublishedPropertyType propertyType) =>
            PropertyCacheLevel.Snapshot;

        public override bool? IsValue(object value, PropertyValueLevel level) => value?.ToString() != "[]";

        public override object ConvertSourceToIntermediate(IPublishedElement owner, IPublishedPropertyType propertyType,
            object source, bool preview) => source?.ToString();

        public override object ConvertIntermediateToObject(IPublishedElement owner, IPublishedPropertyType propertyType,
            PropertyCacheLevel referenceCacheLevel, object inter, bool preview)
        {
            var maxNumber = GetMaxNumber(propertyType.DataType.Configuration);
            if (inter == null)
            {
                return maxNumber == 1 ? null : Enumerable.Empty<FaLink>();
            }
            var faLinks = new List<FaLink>();
            var faLinkDtos = JsonConvert.DeserializeObject<IEnumerable<FaLinkDto>>(inter.ToString()).ToList();
        
            foreach (var faLinkDto in faLinkDtos)
            {
                var links = new List<Link>();
                foreach (var dto in faLinkDto.Link)
                {
                    var type = LinkType.External;
                    var url = dto.Url;

                    if (dto.Udi != null)
                    {
                        type = dto.Udi.EntityType == UdiEntityType.Media
                            ? LinkType.Media
                            : LinkType.Content;

                        if (!_publishedSnapshotAccessor.TryGetPublishedSnapshot(out var publishedSnapshot)) continue;

                        var content = type == LinkType.Media
                            ? publishedSnapshot.Media.GetById(preview, dto.Udi.Guid)
                            : publishedSnapshot.Content.GetById(preview, dto.Udi.Guid);

                        if (content == null || content.ItemType == PublishedItemType.Element)
                        {
                            continue;
                        }

                        url = content.UrlSegment;
                    }

                    links.Add(
                        new Link
                        {
                            Name = dto.Name,
                            Target = dto.Target,
                            Type = type,
                            Udi = dto.Udi,
                            Url = url + dto.QueryString,
                        }
                    );
                }
                faLinks.Add(new FaLink(faLinkDto,links));
            }
            if (maxNumber == 1) return faLinks.FirstOrDefault();
            if (maxNumber > 0) return faLinks.Take(maxNumber);
            return faLinks;
        }

        private int GetMaxNumber(object configuration)
        {
            var json = JsonConvert.SerializeObject(configuration);
            var maxNumber = 0;
            var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (config.ContainsKey("maxNumber"))
            {
                int.TryParse(config["maxNumber"].ToString(), out maxNumber);
            }
            return maxNumber;
        }
    }
}
