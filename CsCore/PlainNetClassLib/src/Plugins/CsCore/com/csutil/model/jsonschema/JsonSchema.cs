using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace com.csutil.model.mtvmtv {

    /// <summary> A C# class that can hold a JSON schema, see 
    /// https://json-schema.org/understanding-json-schema/reference/index.html for specifics 
    /// of the structure of the schema. In summary its a recursive structure which describes each property 
    /// of a target model like the type and name of each property/field </summary>
    [Serializable]
    public class JsonSchema {

        /// <summary> Will contain the type like "object", "integer", "array", .. </summary>
        public string type;
        /// <summary> This will contain the concrete name of the model if type is an "Object" </summary>
        public string modelType; // Not part of official schema

        public List<string> required;
        public Dictionary<string, JsonSchema> properties;

        public string title;
        public string description;

        [JsonProperty("default")]
        public string defaultVal;
        public float? minimum;
        public float? maximum;

        public bool? readOnly;
        public bool? writeOnly;
        public bool? mandatory; // Not part of JSON schema and redundant with required list above
        /// <summary> Regex pattern that has to be matched by the field value to be valid</summary>
        public string pattern;
        public string contentType;
        /// <summary> If the field is an object it has a view model itself </summary>

        public List<JsonSchema> items;
        /// <summary> If true items is a set so it can only contain unique items </summary>
        public bool? uniqueItems;
        /// <summary> Controls whether it's valid to have additional items in the array </summary>
        public bool? additionalItems;

        /// <summary> Indicates that the field can only have descrete values </summary>
        [JsonProperty("enum")]
        public string[] contentEnum;

        public static string ToTitle(string varName) { return RegexUtil.SplitCamelCaseString(varName); }

    }

    public enum ContentType { Alphanumeric, Name, Email, Password, Pin, Essay, Color, Date }

}