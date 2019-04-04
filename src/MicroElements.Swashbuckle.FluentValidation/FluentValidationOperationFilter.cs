﻿using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Swagger <see cref="IOperationFilter"/> that applies FluentValidation rules 
    /// for GET parameters bounded from validatable models.
    /// </summary>
    public class FluentValidationOperationFilter : IOperationFilter
    {
        private readonly IValidatorFactory _validatorFactory;
        private readonly ILogger _logger;
        private readonly IReadOnlyList<FluentValidationRule> _rules;

        public FluentValidationOperationFilter(
            [CanBeNull] IValidatorFactory validatorFactory = null,
            [CanBeNull] IEnumerable<FluentValidationRule> rules = null,
            [CanBeNull] ILoggerFactory loggerFactory = null)
        {
            _validatorFactory = validatorFactory;
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules));
            _rules = FluentValidationRules.CreateDefaultRules();
            if (rules != null)
            {
                var ruleMap = _rules.ToDictionary(rule => rule.Name, rule => rule);
                foreach (var rule in rules)
                {
                    // Add or replace rule
                    ruleMap[rule.Name] = rule;
                }
                _rules = ruleMap.Values.ToList();
            }
        }

        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            try
            {
                ApplyInternal(operation, context);
            }
            catch (Exception e)
            {
                _logger?.LogWarning(0, e, $"Error on apply rules for operation '{operation.OperationId}'.");
            }
        }

        void ApplyInternal(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
                return;

            foreach (var operationParameter in operation.Parameters)
            {
                var apiParameterDescription = context.ApiDescription.ParameterDescriptions.FirstOrDefault(description =>
                    description.Name.Equals(operationParameter.Name, StringComparison.InvariantCultureIgnoreCase));

                var modelMetadata = apiParameterDescription?.ModelMetadata;
                if (modelMetadata != null)
                {
                    var parameterType = modelMetadata.ContainerType;
                    if(parameterType==null)
                        continue;
                    var validator = _validatorFactory.GetValidator(parameterType);
                    if (validator == null)
                        continue;

                    var key = modelMetadata.PropertyName;
                    var validatorsForMember = validator.GetValidatorsForMemberIgnoreCase(key);

                    OpenApiSchema schema = null;
                    foreach (var propertyValidator in validatorsForMember)
                    {
                        foreach (var rule in _rules)
                        {
                            if (rule.Matches(propertyValidator))
                            {
                                try
                                {
                                    if (!context.SchemaRepository.Schemas.TryGetValue(parameterType.Name, out schema))
                                    {
                                        schema = context.SchemaGenerator.GenerateSchema(parameterType, context.SchemaRepository);
                                        context.SchemaRepository.AddSchemaFor(parameterType, schema);
                                    }

                                    rule.Apply(new RuleContext(schema, new SchemaFilterContext(parameterType, null, context.SchemaRepository, context.SchemaGenerator), key.ToLowerCamelCase(), propertyValidator));
                                }
                                catch (Exception e)
                                {
                                    _logger?.LogWarning(0, e, $"Error on apply rule '{rule.Name}' for key '{key}'.");
                                }
                            }
                        }
                    }

                    if (schema?.Required != null)
                        operationParameter.Required = schema.Required.Contains(key, StringComparer.InvariantCultureIgnoreCase);

                    if (schema?.Properties != null)
                    {
                        throw new NotImplementedException("PartialSchema");
                        //todo:v5
                        //if (operationParameter is PartialSchema partialSchema)
                        //{
                        //    if (schema.Properties.TryGetValue(key.ToLowerCamelCase(), out var property) 
                        //        || schema.Properties.TryGetValue(key, out property))
                        //    {
                        //        partialSchema.MinLength = property.MinLength;
                        //        partialSchema.MaxLength = property.MaxLength;
                        //        partialSchema.Pattern = property.Pattern;
                        //        partialSchema.Minimum = property.Minimum;
                        //        partialSchema.Maximum = property.Maximum;
                        //        partialSchema.ExclusiveMaximum = property.ExclusiveMaximum;
                        //        partialSchema.ExclusiveMinimum = property.ExclusiveMinimum;
                        //    }
                        //}
                    }
                }
            }  
        }
    }
}