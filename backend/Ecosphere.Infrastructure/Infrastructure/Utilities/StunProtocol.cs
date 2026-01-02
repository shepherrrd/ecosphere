using System.Net;

namespace Ecosphere.Infrastructure.Infrastructure.Utilities;

// STUN Message Types (RFC 5389)
public enum StunMessageType : ushort
{
    BindingRequest = 0x0001,
    BindingResponse = 0x0101,
    BindingErrorResponse = 0x0111,
    SharedSecretRequest = 0x0002,
    SharedSecretResponse = 0x0102,
    SharedSecretErrorResponse = 0x0112
}

// STUN Attribute Types
public enum StunAttributeType : ushort
{
    MappedAddress = 0x0001,
    ResponseAddress = 0x0002,
    ChangeRequest = 0x0003,
    SourceAddress = 0x0004,
    ChangedAddress = 0x0005,
    Username = 0x0006,
    Password = 0x0007,
    MessageIntegrity = 0x0008,
    ErrorCode = 0x0009,
    UnknownAttributes = 0x000A,
    ReflectedFrom = 0x000B,
    Realm = 0x0014,
    Nonce = 0x0015,
    XorMappedAddress = 0x0020,
    Software = 0x8022,
    AlternateServer = 0x8023,
    Fingerprint = 0x8028
}

// STUN Message Structure
public class StunMessage
{
    public StunMessageType MessageType { get; set; }
    public ushort MessageLength { get; set; }
    public byte[] TransactionId { get; set; } = new byte[16];
    public List<StunAttribute> Attributes { get; set; } = new();

    public const uint MagicCookie = 0x2112A442;

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Message Type (2 bytes)
        writer.Write(IPAddress.HostToNetworkOrder((short)MessageType));

        // Calculate message length (sum of all attribute lengths)
        ushort totalLength = 0;
        foreach (var attr in Attributes)
        {
            totalLength += (ushort)(4 + attr.Length); // 4 bytes header + attribute length
        }

        // Message Length (2 bytes)
        writer.Write(IPAddress.HostToNetworkOrder((short)totalLength));

        // Magic Cookie (4 bytes)
        writer.Write(IPAddress.HostToNetworkOrder((int)MagicCookie));

        // Transaction ID (12 bytes after magic cookie in RFC 5389)
        writer.Write(TransactionId, 0, 12);

        // Attributes
        foreach (var attr in Attributes)
        {
            writer.Write(IPAddress.HostToNetworkOrder((short)attr.Type));
            writer.Write(IPAddress.HostToNetworkOrder((short)attr.Length));
            writer.Write(attr.Value);

            // Padding to 4-byte boundary
            int padding = (4 - (attr.Length % 4)) % 4;
            for (int i = 0; i < padding; i++)
                writer.Write((byte)0);
        }

        return ms.ToArray();
    }

    public static StunMessage? Parse(byte[] data)
    {
        try
        {
            if (data.Length < 20) return null;

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            var message = new StunMessage();

            // Message Type
            message.MessageType = (StunMessageType)IPAddress.NetworkToHostOrder(reader.ReadInt16());

            // Message Length
            message.MessageLength = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());

            // Magic Cookie
            var magicCookie = (uint)IPAddress.NetworkToHostOrder(reader.ReadInt32());
            if (magicCookie != MagicCookie) return null;

            // Transaction ID (12 bytes)
            message.TransactionId = new byte[16];
            Array.Copy(BitConverter.GetBytes(MagicCookie), 0, message.TransactionId, 0, 4);
            reader.Read(message.TransactionId, 4, 12);

            // Parse Attributes
            while (ms.Position < ms.Length && ms.Position < 20 + message.MessageLength)
            {
                if (ms.Length - ms.Position < 4) break;

                var attrType = (StunAttributeType)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                var attrLength = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());

                if (ms.Length - ms.Position < attrLength) break;

                var attrValue = reader.ReadBytes(attrLength);

                // Skip padding
                int padding = (4 - (attrLength % 4)) % 4;
                reader.ReadBytes(padding);

                message.Attributes.Add(new StunAttribute
                {
                    Type = attrType,
                    Length = attrLength,
                    Value = attrValue
                });
            }

            return message;
        }
        catch
        {
            return null;
        }
    }
}

public class StunAttribute
{
    public StunAttributeType Type { get; set; }
    public ushort Length { get; set; }
    public byte[] Value { get; set; } = Array.Empty<byte>();

    public static StunAttribute CreateXorMappedAddress(IPEndPoint endpoint, byte[] transactionId)
    {
        var attr = new StunAttribute
        {
            Type = StunAttributeType.XorMappedAddress
        };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Reserved byte
        writer.Write((byte)0);

        // Family (IPv4 = 0x01, IPv6 = 0x02)
        byte family = endpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? (byte)0x01 : (byte)0x02;
        writer.Write(family);

        // XOR Port
        ushort xorPort = (ushort)(endpoint.Port ^ (StunMessage.MagicCookie >> 16));
        writer.Write(IPAddress.HostToNetworkOrder((short)xorPort));

        // XOR Address
        var addressBytes = endpoint.Address.GetAddressBytes();
        var magicCookieBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)StunMessage.MagicCookie));

        for (int i = 0; i < addressBytes.Length && i < 4; i++)
        {
            writer.Write((byte)(addressBytes[i] ^ magicCookieBytes[i]));
        }

        attr.Value = ms.ToArray();
        attr.Length = (ushort)attr.Value.Length;

        return attr;
    }

    public static StunAttribute CreateMappedAddress(IPEndPoint endpoint)
    {
        var attr = new StunAttribute
        {
            Type = StunAttributeType.MappedAddress
        };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Reserved byte
        writer.Write((byte)0);

        // Family
        byte family = endpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? (byte)0x01 : (byte)0x02;
        writer.Write(family);

        // Port
        writer.Write(IPAddress.HostToNetworkOrder((short)endpoint.Port));

        // Address
        writer.Write(endpoint.Address.GetAddressBytes());

        attr.Value = ms.ToArray();
        attr.Length = (ushort)attr.Value.Length;

        return attr;
    }

    public static StunAttribute CreateSoftware(string software)
    {
        var attr = new StunAttribute
        {
            Type = StunAttributeType.Software,
            Value = System.Text.Encoding.UTF8.GetBytes(software)
        };
        attr.Length = (ushort)attr.Value.Length;
        return attr;
    }
}
