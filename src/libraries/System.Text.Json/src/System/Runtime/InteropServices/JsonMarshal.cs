// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// An unsafe class that provides a set of methods to access the underlying data representations of JSON types.
    /// </summary>
    public static class JsonMarshal
    {
        /// <summary>
        /// Gets a <see cref="ReadOnlySpan{T}"/> view over the raw JSON data of the given <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="element">The JSON element from which to extract the span.</param>
        /// <returns>The span containing the raw JSON data of<paramref name="element"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying <see cref="JsonDocument"/> has been disposed.</exception>
        /// <remarks>
        /// While the method itself does check for disposal of the underlying <see cref="JsonDocument"/>,
        /// it is possible that it could be disposed after the method returns, which would result in
        /// the span pointing to a buffer that has been returned to the shared pool. Callers should take
        /// extra care to make sure that such a scenario isn't possible to avoid potential data corruption.
        /// </remarks>
        public static ReadOnlySpan<byte> GetRawUtf8Value(JsonElement element)
        {
            return element.GetRawValue().Span;
        }

        /// <summary>
        /// Gets a <see cref="ReadOnlySpan{T}"/> view over the raw JSON data of the given <see cref="JsonProperty"/> name.
        /// </summary>
        /// <param name="property">The JSON property from which to extract the span.</param>
        /// <returns>The span containing the raw JSON data of the <paramref name="property"/> name. This will not include the enclosing quotes.</returns>
        /// <exception cref="ObjectDisposedException">The underlying <see cref="JsonDocument"/> has been disposed.</exception>
        /// <remarks>
        /// <para>
        /// While the method itself does check for disposal of the underlying <see cref="JsonDocument"/>,
        /// it is possible that it could be disposed after the method returns, which would result in
        /// the span pointing to a buffer that has been returned to the shared pool. Callers should take
        /// extra care to make sure that such a scenario isn't possible to avoid potential data corruption.
        /// </para>
        /// </remarks>
        public static ReadOnlySpan<byte> GetRawUtf8PropertyName(JsonProperty property)
        {
            return property.NameSpan;
        }

        /// <summary>
        /// Indicates whether the raw UTF8 value of a JSON element requires unescaping, providing the minimum required
        /// buffer size for the unescaped result, and the zero-based index of the first character that requires unescaping
        /// if the value requires unescaping.
        /// </summary>
        /// <param name="element">The JSON element whose raw value may require unescaping.</param>
        /// <param name="indexOfFirstEscapedSequence">The zero-based index of the first character that requires unescaping, or <c>-1</c> if no unescaping is required.</param>
        /// <param name="minimumUnescapedBufferSize">The minimum length of buffer required for the unescaped value.</param>
        /// <returns><see langword="true"/> if the value requires unescaping, otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// <para>
        /// If the value requires unescaping, then the <paramref name="indexOfFirstEscapedSequence"/> will be set to the index of the start of the first UTF8 byte
        /// sequence that requires unescaping, and the <paramref name="minimumUnescapedBufferSize"/> will be set to the minimum buffer size required to hold the unescaped value.
        /// Otherwise they will be set to <c>-1</c> and <c>0</c> respectively.
        /// </para>
        /// <para>
        /// If unescaping is required, you can create an appropriately sized UTF8 byte buffer, and pass these to <see cref="TryUnescapeRawUtf8Value(JsonElement, Span{byte}, int, out int)"/> to unescape the value.
        /// </para>
        /// </remarks>
        public static bool Utf8ValueRequiresUnescaping(JsonElement element, out int indexOfFirstEscapedSequence, out int minimumUnescapedBufferSize)
        {
            if (element.ValueIsEscaped)
            {
                ReadOnlySpan<byte> span = element.GetRawValue().Span;
                indexOfFirstEscapedSequence = span.IndexOf(JsonConstants.BackSlash);
                if (indexOfFirstEscapedSequence >= 0)
                {
                    minimumUnescapedBufferSize = span.Length;
                    return true;
                }
            }

            indexOfFirstEscapedSequence = -1;
            minimumUnescapedBufferSize = 0;
            return false;
        }

        /// <summary>
        /// Writes the unescaped UTF8 value to the supplied buffer.
        /// </summary>
        /// <param name="element">The element whose raw UTF8 value requires unescaping.</param>
        /// <param name="unescapedValue">The buffer into which to write the unescaped value.</param>
        /// <param name="indexOfFirstEscapedSequence">The index of the start of the first UTF8 byte sequence that requires unescaping.</param>
        /// <param name="written">The number of bytes written to the buffer.</param>
        /// <returns><see langword="true"/> if the value was successfully unescaped.</returns>
        public static bool TryUnescapeRawUtf8Value(JsonElement element, Span<byte> unescapedValue, int indexOfFirstEscapedSequence, out int written)
        {
            return JsonReaderHelper.TryUnescape(element.GetRawValue().Span, unescapedValue, indexOfFirstEscapedSequence, out written);
        }

        /// <summary>
        /// Gets the required buffer size to transcode the raw UTF8 value of a JSON element into a character array,
        /// along with a value which indicates whether the value will require unescaping before transcoding.
        /// </summary>
        /// <param name="element">The JSON element whose raw value is to be unescaped and transcoded.</param>
        /// <param name="indexOfFirstEscapedSequence">The zero-based index of the first UTF8 byte sequence that requires unescaping, or <c>-1</c> if no unescaping is required.</param>
        /// <param name="minimumTranscodedBufferSize">The minimum length of buffer required for the transcoded value.</param>
        /// <remarks>
        /// <para>
        /// If the value requires unescaping, then the <paramref name="indexOfFirstEscapedSequence"/> will be set to the index of the start of the first UTF8 byte
        /// sequence that requires unescaping, and the <paramref name="minimumTranscodedBufferSize"/> will be set to the minimum buffer size required to hold the unescaped and transcoded value.
        /// Otherwise they will be set to <c>-1</c> and <c>0</c> respectively.
        /// </para>
        /// <para>
        /// You can pass these to <see cref="TryTranscodeRawUtf8Value(JsonElement, Span{char}, int, out int)"/> to unescape and transcode the raw value.
        /// </para>
        /// </remarks>
        public static void GetTranscodingDetailsForRawUtf8Value(JsonElement element, out int indexOfFirstEscapedSequence, out int minimumTranscodedBufferSize)
        {
            ReadOnlySpan<byte> span = element.GetRawValue().Span;

            if (element.ValueIsEscaped)
            {
                indexOfFirstEscapedSequence = span.IndexOf(JsonConstants.BackSlash);
            }
            else
            {
                indexOfFirstEscapedSequence = -1;
            }

            minimumTranscodedBufferSize = span.Length;
        }

        /// <summary>
        /// Unescapes the raw UTF8 value (if required), and transcodes into the supplied character array buffer.
        /// </summary>
        /// <param name="element">The element whose raw UTF8 value is to be unescaped and transcoded.</param>
        /// <param name="transcodedValue">The buffer into which to write the unescaped and transcoded value.</param>
        /// <param name="indexOfFirstEscapedSequence">The index of the start of the first UTF8 byte sequence that requires unescaping,
        /// or <c>-1</c> if unescaping is not required.</param>
        /// <param name="written">The number of characters written to the buffer.</param>
        /// <returns><see langword="true"/> if the raw value was successfully unescaped and transcoded.</returns>
        /// <remarks>
        /// <para>
        ///   You should call <see cref="GetTranscodingDetailsForRawUtf8Value(JsonElement, out int, out int)"/> to determine
        ///   the minimum <paramref name="transcodedValue"/> buffer size, and the index of the first escaped sequence (if any).
        /// </para>
        /// </remarks>
        public static bool TryTranscodeRawUtf8Value(JsonElement element, Span<char> transcodedValue, int indexOfFirstEscapedSequence, out int written)
        {
            return JsonReaderHelper.TryGetUnescapedString(element.GetRawValue().Span, transcodedValue, indexOfFirstEscapedSequence, out written);
        }

        /// <summary>
        /// Indicates whether the raw UTF8 name of a JSON property requires unescaping, providing the minimum required
        /// buffer size for the unescaped result, and the zero-based index of the first character that requires unescaping
        /// if the name requires unescaping.
        /// </summary>
        /// <param name="property">The JSON property whose raw name may require unescaping.</param>
        /// <param name="indexOfFirstEscapedSequence">The zero-based index of the first character that requires unescaping, or <c>-1</c> if no unescaping is required.</param>
        /// <param name="minimumUnescapedBufferSize">The minimum length of buffer required for the unescaped name.</param>
        /// <returns><see langword="true"/> if the value requires unescaping, otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// <para>
        /// If the value requires unescaping, then the <paramref name="indexOfFirstEscapedSequence"/> will be set to the index of the start of the first UTF8 byte
        /// sequence that requires unescaping, and the <paramref name="minimumUnescapedBufferSize"/> will be set to the minimum buffer size required to hold the unescaped value.
        /// Otherwise they will be set to <c>-1</c> and <c>0</c> respectively.
        /// </para>
        /// <para>
        /// If unescaping is required, you can create an appropriately sized UTF8 byte buffer, and pass these to <see cref="TryUnescapeRawUtf8PropertyName(JsonProperty, Span{byte}, int, out int)"/> to unescape the value.
        /// </para>
        /// </remarks>
        public static bool Utf8PropertyNameRequiresUnescaping(JsonProperty property, out int indexOfFirstEscapedSequence, out int minimumUnescapedBufferSize)
        {
            if (property.NameIsEscaped)
            {
                ReadOnlySpan<byte> span = property.NameSpan;
                indexOfFirstEscapedSequence = span.IndexOf(JsonConstants.BackSlash);
                if (indexOfFirstEscapedSequence >= 0)
                {
                    minimumUnescapedBufferSize = span.Length;
                    return true;
                }
            }

            indexOfFirstEscapedSequence = -1;
            minimumUnescapedBufferSize = 0;
            return false;
        }

        /// <summary>
        /// Writes the unescaped UTF8 property name to the supplied buffer.
        /// </summary>
        /// <param name="property">The property whose raw UTF8 name requires unescaping.</param>
        /// <param name="unescapedValue">The buffer into which to write the unescaped value.</param>
        /// <param name="indexOfFirstEscapedSequence">The index of the start of the first UTF8 byte sequence that requires unescaping.</param>
        /// <param name="written">The number of bytes written to the buffer.</param>
        /// <returns><see langword="true"/> if the name was successfully unescaped.</returns>
        public static bool TryUnescapeRawUtf8PropertyName(JsonProperty property, Span<byte> unescapedValue, int indexOfFirstEscapedSequence, out int written)
        {
            return JsonReaderHelper.TryUnescape(property.NameSpan, unescapedValue, indexOfFirstEscapedSequence, out written);
        }

        /// <summary>
        /// Gets the required buffer size to transcode the raw UTF8 name of a JSON property into a character array,
        /// along with a value which indicates whether the name will require unescaping before transcoding.
        /// </summary>
        /// <param name="property">The JSON property whose raw name is to be unescaped and transcoded.</param>
        /// <param name="indexOfFirstEscapedSequence">The zero-based index of the first UTF8 byte sequence that requires unescaping, or <c>-1</c> if no unescaping is required.</param>
        /// <param name="minimumTranscodedBufferSize">The minimum length of buffer required for the transcoded name.</param>
        /// <remarks>
        /// <para>
        /// If the name requires unescaping, then the <paramref name="indexOfFirstEscapedSequence"/> will be set to the index of the start of the first UTF8 byte
        /// sequence that requires unescaping, and the <paramref name="minimumTranscodedBufferSize"/> will be set to the minimum buffer size required to hold the unescaped and transcoded name.
        /// Otherwise they will be set to <c>-1</c> and <c>0</c> respectively.
        /// </para>
        /// <para>
        /// You can pass these to <see cref="TryTranscodeRawUtf8PropertyName(JsonElement, Span{char}, int, out int)"/> to unescape and transcode the raw name.
        /// </para>
        /// </remarks>
        public static void GetTranscodingDetailsForRawUtf8PropertyName(JsonElement property, out int indexOfFirstEscapedSequence, out int minimumTranscodedBufferSize)
        {
            ReadOnlySpan<byte> span = property.GetRawValue().Span;

            if (property.ValueIsEscaped)
            {
                indexOfFirstEscapedSequence = span.IndexOf(JsonConstants.BackSlash);
            }
            else
            {
                indexOfFirstEscapedSequence = -1;
            }

            minimumTranscodedBufferSize = span.Length;
        }

        /// <summary>
        /// Transcodes the raw UTF8 property name into the supplied character array buffer, unescaping if required.
        /// </summary>
        /// <param name="element">The element whose raw UTF8 value is to be unescaped and transcoded.</param>
        /// <param name="transcodedValue">The buffer into which to write the unescaped and transcoded value.</param>
        /// <param name="indexOfFirstEscapedSequence">The index of the start of the first UTF8 byte sequence that requires unescaping,
        /// or <c>-1</c> if unescaping is not required.</param>
        /// <param name="written">The number of characters written to the buffer.</param>
        /// <returns><see langword="true"/> if the raw value was successfully unescaped and transcoded.</returns>
        /// <remarks>
        /// <para>
        ///   You should call <see cref="GetTranscodingDetailsForRawUtf8PropertyName(JsonElement, out int, out int)"/> to determine
        ///   the minimum <paramref name="transcodedValue"/> buffer size, and the index of the first escaped sequence (if any).
        /// </para>
        /// <para>
        /// Note that you can transcode the escaped value if you pass -1 as the <paramref name="indexOfFirstEscapedSequence"/>.
        /// This disables the unescape step.
        /// </para>
        /// </remarks>
        public static bool TryTranscodeRawUtf8PropertyName(JsonElement element, Span<char> transcodedValue, int indexOfFirstEscapedSequence, out int written)
        {
            return JsonReaderHelper.TryGetUnescapedString(element.GetRawValue().Span, transcodedValue, indexOfFirstEscapedSequence, out written);
        }
    }
}
