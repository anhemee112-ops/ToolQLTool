import java.io.*;
/** In "chỉ số-hằng-String độ-dài" cho mọi hằng chuỗi trong một file .class (javap không in được ký tự điều khiển). */
public class StrLens { public static void main(String[] a) throws Exception {
 DataInputStream in=new DataInputStream(new BufferedInputStream(new FileInputStream(a[0])));
 in.readInt(); in.readUnsignedShort(); in.readUnsignedShort(); int n=in.readUnsignedShort();
 String[] u=new String[n]; int[] sref=new int[n];
 for(int i=1;i<n;i++){ int t=in.readUnsignedByte();
  switch(t){ case 1: u[i]=in.readUTF(); break; case 3: case 4: in.readInt(); break; case 5: case 6: in.readLong(); i++; break;
   case 7: case 8: sref[i]=in.readUnsignedShort(); break; case 9: case 10: case 11: case 12: in.readInt(); break;
   case 15: in.readByte(); in.readUnsignedShort(); break; case 16: in.readUnsignedShort(); break; case 17: case 18: in.readInt(); break; case 19: case 20: in.readUnsignedShort(); break;
   default: throw new Exception("tag "+t);} }
 for(int i=1;i<n;i++) if(sref[i]!=0 && u[sref[i]]!=null) System.out.println(i+" "+u[sref[i]].length());
}}
