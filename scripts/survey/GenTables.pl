# Sinh hai bảng markdown từ cases.tsv và senders.tsv
sub short { my $s=shift; $s=~s/UnsignedByte/ubyte/g; $s=~s/UnsignedShort/ushort/g; $s=~s/Fully/(đọc hết)/; $s=lc($s); $s=~s/,/, /g; $s }
print "## Bảng server → client (`cj_0.b`)\n\n| Mã | Dòng | Trường đọc trực tiếp (theo thứ tự trong mã) | Hàm con |\n|---|---|---|---|\n";
open F,"cases.tsv"; my @r=map{chomp;[split /\t/,$_,4]}<F>;
for(sort{$a->[0]<=>$b->[0] || $a->[1]<=>$b->[1]}@r){ my($c,$l,$o,$h)=@$_; my $n=$o eq "" ? 0 : ($o=~tr/,//)+1; $o=short($o); $o=substr($o,0,100)."… ($n trường)" if length($o)>110; $h=~s/\(var1_1\)//g; $h=~s/ /, /g; print "| $c | $l | $o | $h |\n"; }
print "\n## Bảng client → server (lớp `an`)\n\nMột hàm có thể gửi nhiều mã (ví dụ tấn công). Cột \"Trường ghi\" gộp mọi lệnh ghi trong hàm.\n\n| Mã | Hàm gửi (tên trong jar) | Trường ghi |\n|---|---|---|\n";
open G,"senders.tsv"; my @s=map{chomp;[split /\t/,$_,3]}<G>;
for(sort{ ($a->[0]=~/^(-?\d+)/)[0] <=> ($b->[0]=~/^(-?\d+)/)[0] || $a->[0] cmp $b->[0] }@s){ my($c,$m,$w)=@$_; $w=short($w); $w=substr($w,0,90)."…" if length($w)>95; $m=~s/\|/\|/g; print "| $c | `$m` | $w |\n"; }
