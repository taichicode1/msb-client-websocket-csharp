﻿// <copyright file="Service.cs" company="Fraunhofer Institute for Manufacturing Engineering and Automation IPA">
// Copyright 2019 Fraunhofer Institute for Manufacturing Engineering and Automation IPA
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace Fraunhofer.IPA.MSB.Client.API.Model
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Fraunhofer.IPA.MSB.Client.API.Attributes;
    using Fraunhofer.IPA.MSB.Client.API.Configuration;
    using Fraunhofer.IPA.MSB.Client.API.Exceptions;
    using Fraunhofer.IPA.MSB.Client.API.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents an MSB service.
    /// </summary>
    public abstract class Service
    {
        private static readonly ILog Log = LogProvider.For<Service>();

        private Dictionary<AbstractFunctionHandler, List<Function>> registeredFunctionHandlerAndRelatedFunctions = new Dictionary<AbstractFunctionHandler, List<Function>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class.
        /// </summary>
        /// <param name="uuid">The <see cref="Service.Uuid"/> of the <see cref="Service"/>.</param>
        /// <param name="name">The <see cref="Service.Name"/> of the <see cref="Service"/>.</param>
        /// <param name="description">The <see cref="Service.Description"/> of the <see cref="Service"/>.</param>
        /// <param name="token">The <see cref="Service.Token"/> of the <see cref="Service"/>.</param>
        public Service(string uuid, string name, string description, string token)
        {
            this.Name = name;
            this.Description = description;
            this.Uuid = uuid;
            this.Token = token;
        }

        /// <summary>Gets or sets name of the service.</summary>
        [JsonProperty("name")]
        public string Name { get; protected set; }

        /// <summary>Gets or sets description of the service.</summary>
        [JsonProperty("description")]
        public string Description { get; protected set; }

        /// <summary>Gets or sets token to verify the service in the MSB GUI.</summary>
        [JsonProperty("token")]
        public string Token { get; protected set; }

        /// <summary>Gets or sets UUID of the service.</summary>
        [JsonProperty("uuid")]
        public string Uuid { get; protected set; }

        /// <summary>Gets or sets configuration of the service.</summary>
        [JsonProperty("configuration")]
        public Configuration Configuration { get; protected set; } = new Configuration();

        /// <summary>Gets or sets events of the service.</summary>
        [JsonProperty("events")]
        public List<Event> Events { get; protected set; } = new List<Event>();

        /// <summary>Gets or sets functions of the service.</summary>
        [JsonProperty("functions")]
        public List<Function> Functions { get; protected set; } = new List<Function>();

        /// <summary>Gets the class type of the service.</summary>
        [JsonProperty("@class")]
        public abstract string Class
        {
            get;
        }

        /// <summary>
        /// Adds an event to the service.
        /// </summary>
        /// <param name="eventToAdd">Event that should be added.</param>
        public void AddEvent(Event eventToAdd)
        {
            // TODO: Check if event already exists
            this.Events.Add(eventToAdd);
            Log.Debug($"Added Event '{eventToAdd.Id}'");
        }

        /// <summary>
        /// Removes an event from the service.
        /// </summary>
        /// <param name="eventToRemove">Event that should be removed.</param>
        public void RemoveEvent(Event eventToRemove)
        {
            // TODO: Check if event is not needed as response event of a function.
            this.Events.Remove(eventToRemove);
            Log.Debug($"Removed Event '{eventToRemove.Id}'");
        }

        /// <summary>
        /// Adds a event using JSON.
        /// </summary>
        /// <param name="eventDescription">The event description as JSON.</param>
        public void AddEventRaw(string eventDescription)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds a function the service.
        /// </summary>
        /// <param name="function">Function that should be added.</param>
        public void AddFunction(Function function)
        {
            foreach (var responseEventId in function.ResponseEventIds)
            {
                Event responseEvent = this.Events.Find(e => e.Id == responseEventId);
                if (responseEvent == null)
                {
                    Log.Warn("Response Event not found");
                    throw new ResponseEventNotFoundException($"ResponseEvent '{responseEventId}' of service '{this.Uuid}' not found");
                }
            }

            this.Functions.Add(function);
        }

        /// <summary>
        /// Removes a function from the service.
        /// </summary>
        /// <param name="functionToRemove">Function that should be removed.</param>
        public void RemoveFunction(Function functionToRemove)
        {
            this.Functions.Remove(functionToRemove);
            Log.Debug($"Removed Function '{functionToRemove.Id}'");
        }

        /// <summary>
        /// Adds the function handler to the service. All methods defined as MSB function via <see cref="MsbFunctionAttribute"/> will be added as <see cref="Function"/>s.
        /// </summary>
        /// <param name="functionHandler">Function handler that should be added.</param>
        public void AddFunctionHandler(AbstractFunctionHandler functionHandler)
        {
            if (this.registeredFunctionHandlerAndRelatedFunctions.ContainsKey(functionHandler))
            {
                Log.Info($"Nothing to do, FunctionHandler '{functionHandler.GetType().FullName}' already registered to Service '{this.Uuid}'");
            }
            else
            {
                List<Function> addedFunctions = new List<Function>();
                foreach (var functionHandlerMethod in functionHandler.GetType().GetRuntimeMethods())
                {
                    if (!functionHandlerMethod.IsPublic || functionHandlerMethod.DeclaringType == typeof(object))
                    {
                        continue;
                    }

                    Function function = new Function(functionHandler, functionHandlerMethod);
                    this.AddFunction(function);
                    addedFunctions.Add(function);
                }

                this.registeredFunctionHandlerAndRelatedFunctions.Add(functionHandler, addedFunctions);
                Log.Info($"FunctionHandler '{functionHandler.GetType().FullName}' registered to Service '{this.Uuid}'");
            }
        }

        /// <summary>
        /// Removes the function handler to the service. All MSB function that belong to this function handler will be removed.
        /// </summary>
        /// <param name="functionHandler">The function handler.</param>
        public void RemoveFunctionHandler(AbstractFunctionHandler functionHandler)
        {
            if (this.registeredFunctionHandlerAndRelatedFunctions.ContainsKey(functionHandler))
            {
                List<Function> addedFunctions = this.registeredFunctionHandlerAndRelatedFunctions[functionHandler];
                foreach (var function in addedFunctions)
                {
                    this.RemoveFunction(function);
                }

                this.registeredFunctionHandlerAndRelatedFunctions.Remove(functionHandler);
                Log.Info($"FunctionHandler '{functionHandler.GetType().FullName}' removed from Service '{this.Uuid}'");
            }
            else
            {
                Log.Info($"Nothing to do, FunctionHandler '{functionHandler.GetType().FullName}' not registered to Service '{this.Uuid}'");
            }
        }

        /// <summary>
        /// Adds a configuration parameter to the service.
        /// </summary>
        /// <param name="name">Name of the added configuration parameter.</param>
        /// <param name="value">Value of the added configuration parameter.</param>
        public void AddConfigurationParameter(string name, ConfigurationParameterValue value)
        {
            this.Configuration.Parameters.Add(name, value);
            Log.Debug($"Added Configuration Parameter '{name}' with value '{JsonConvert.SerializeObject(value)}'´from service '{this.Uuid}'");
        }

        /// <summary>
        /// Removes a configuration parameter from the service.
        /// </summary>
        /// <param name="name">Name of configuration parameter that should be removed.</param>
        public void RemoveConfigurationParameter(string name)
        {
            this.Configuration.Parameters.Remove(name);
            Log.Debug($"Removed Configuration Parameter '{name}' from service '{this.Uuid}'");
        }

        /// <summary>
        /// Generates the <see cref="Event.AtId"/>s of the events and uses them as references in <see cref="Function.ResponseEvents"/> in all <see cref="Function"/>s of this service.
        /// </summary>
        public void GenerateAtIds()
        {
            // Generate ids
            for (int i = 0; i < this.Events.Count; i++)
            {
                this.Events[i].AtId = i;
            }

            // Use the generated ids as (JSON) references for response events in the functions of the service
            foreach (var function in this.Functions)
            {
                foreach (var responseEventId in function.ResponseEventIds)
                {
                    Event responseEvent = this.Events.Find(e => e.Id == responseEventId);
                    function.ResponseEvents.Add(responseEvent.AtId);
                }
            }
        }

        /// <summary>
        /// Gets an event by id.
        /// </summary>
        /// <param name="eventId">The id of the <see cref="Event"/></param>
        /// <returns>The event found for the id.</returns>
        public Event GetEventById(string eventId)
        {
            return this.Events.Find(e => e.Id == eventId);
        }

        /// <summary>
        /// Gets a function by id.
        /// </summary>
        /// <param name="functionId">The id of the <see cref="Function"/>.</param>
        /// <returns>The function found for the id.</returns>
        public Function GetFunctionById(string functionId)
        {
            return this.Functions.Find(f => f.Id == functionId);
        }

        /// <summary>
        /// Converts to Object JSON string.
        /// </summary>
        /// <returns>Object as JSON string.</returns>
        public string ToJson()
        {
            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                PreserveReferencesHandling = PreserveReferencesHandling.None
            };

            return JsonConvert.SerializeObject(this, Formatting.None, settings: jsonSerializerSettings);
        }
    }
}
