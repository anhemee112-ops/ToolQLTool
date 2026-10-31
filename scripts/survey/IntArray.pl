# Mô phỏng ngăn xếp phương thức khởi tạo mảng int trong file javap -c: perl intarr.pl file.txt tênPhươngThức
# Hỗ trợ phép toán số học/bit và ldc chuỗi + String.length. Gặp lệnh lạ thì báo ra stderr và dừng.
my($f,$meth,$lensf)=@ARGV; open F,"<$f"; my $in=0; my @s; my %a; my %len;
if($lensf){ open L,"<$lensf"; while(<L>){ my($k,$v)=split; $len{$k}=$v } }
sub slen { my $t=shift; my $n=0; while($t ne ''){ if($t=~s/^\\u[0-9a-fA-F]{4}//){$n++} elsif($t=~s/^\\[nrtbf"'\\]//){$n++} else {$t=~s/^.//s;$n++} } $n }
sub i32 { my $v=shift; $v=$v & 0xFFFFFFFF; $v-=4294967296 if $v>=2147483648; $v }
while(my $l=<F>){
 $l=~s/\r?\n$//;
 if(!$in){ $in=1 if $l=~/^\s+(?:private |public )?static void \Q$meth\E\(\)/; next }
 last if $l=~/^\s*$/;
 next unless $l=~/^\s+\d+:\s+(\S+)\s*(.*)$/; my($op,$rest)=($1,$2);
 if($op eq 'iconst_m1'){push @s,-1}
 elsif($op=~/^iconst_(\d)$/){push @s,$1+0}
 elsif($op=~/^(bipush|sipush)$/){ $rest=~/(-?\d+)/; push @s,$1+0 }
 elsif($op eq 'ldc'){ if($rest=~/\/\/ int (-?\d+)/){push @s,$1+0} elsif($rest=~/#(\d+)\s+\/\/ String/){ if(!exists $len{$1}){ print STDERR "thiếu độ dài #$1\n"; exit 1 } push @s,{str=>$len{$1}} } else {print STDERR "ldc lạ: $l\n"; exit 1} }
 elsif($op eq 'invokevirtual' && $rest=~/String.length/){ my $x=pop @s; push @s,$x->{str} }
 elsif($op eq 'newarray'){ pop @s; }
 elsif($op eq 'putstatic'){ }
 elsif($op eq 'getstatic'){ if($rest=~/:\[I/){push @s,'ARR'} else {print STDERR "getstatic lạ: $l\n"; exit 1} }
 elsif($op eq 'dup'){ push @s,$s[-1] }
 elsif($op eq 'iastore'){ my $v=pop @s; my $i=pop @s; pop @s; $a{$i}=$v }
 elsif($op eq 'ineg'){ my $x=pop @s; push @s,i32(-$x) }
 elsif($op=~/^(iadd|isub|imul|ixor|iand|ior|ishl|ishr|iushr)$/){ my $b=pop @s; my $x=pop @s; my $r;
   $r=$x+$b if $op eq 'iadd'; $r=$x-$b if $op eq 'isub'; $r=$x*$b if $op eq 'imul';
   $r=(i32($x)^i32($b)) if $op eq 'ixor'; $r=(i32($x)&i32($b)) if $op eq 'iand'; $r=(i32($x)|i32($b)) if $op eq 'ior';
   $r=$x<<($b&31) if $op eq 'ishl'; $r=$x>>($b&31) if $op eq 'ishr'; $r=(($x&0xFFFFFFFF)>>($b&31)) if $op eq 'iushr';
   push @s,i32($r) }
 elsif($op eq 'i2b'){ my $x=pop @s; $x&=255; $x-=256 if $x>127; push @s,$x }
 elsif($op eq 'i2s'){ my $x=pop @s; $x&=65535; $x-=65536 if $x>32767; push @s,$x }
 elsif($op eq 'return'){ last }
 else { print STDERR "lệnh lạ: $l\n"; exit 1 }
}
print "$_=$a{$_}\n" for sort{$a<=>$b}keys %a;
