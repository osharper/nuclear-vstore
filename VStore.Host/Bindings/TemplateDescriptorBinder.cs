using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.ModelBinding;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Host.Descriptors;

namespace NuClear.VStore.Host.Bindings
{
    public sealed class TemplateDescriptorBinder : IModelBinder
    {
        private const string ElementDescriptorTypeToken = "type";

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var elementDescriptors = new List<IElementDescriptor>();

            var requestBody = GetBodyContent(bindingContext.HttpContext.Request.Body);
            //TODO

            var descriptors = JArray.Parse(requestBody);
            foreach (var descriptor in descriptors)
            {
                var type = descriptor[ElementDescriptorTypeToken].ToString();
                var descriptorType = (ElementDescriptorType)Enum.Parse(typeof(ElementDescriptorType), type);

                var elementDescriptor = Deserialize(new JsonSerializer(), descriptor.CreateReader(), descriptorType);
                if (elementDescriptor == null)
                {
                    bindingContext.Result = ModelBindingResult.Failed();
                    return Task.CompletedTask;
                }

                elementDescriptors.Add(elementDescriptor);
            }

            bindingContext.Result = ModelBindingResult.Success(elementDescriptors);
            return Task.CompletedTask;
        }

        private static string GetBodyContent(Stream body)
        {
            using (var reader = new StreamReader(body))
            {
                return reader.ReadToEnd();
            }
        }

        private static IElementDescriptor Deserialize(JsonSerializer serializer, JsonReader jsonReader, ElementDescriptorType descriptorType)
        {
            switch (descriptorType)
            {
                case ElementDescriptorType.Text:
                    return serializer.Deserialize<TextElementDescriptor>(jsonReader);
                case ElementDescriptorType.Image:
                    return serializer.Deserialize<ImageElementDescriptor>(jsonReader);
                case ElementDescriptorType.Article:
                    return serializer.Deserialize<ArticleElementDescriptor>(jsonReader);
                default:
                    return null;
            }
        }
    }
}