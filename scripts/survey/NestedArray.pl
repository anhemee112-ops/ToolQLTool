# Mô phỏng bytecode dựng mảng lồng nhau (int[][][]) trong khoảng dòng của file javap -c.
# Dùng: perl nestarr.pl file.txt dòngĐầu dòngCuối dG.txt file.lens  -> in JSON của mảng
my($f,$a,$z,$dgf,$lensf)=@ARGV;
my %dG; open D,"<$dgf"; while(<D>){ chomp; my($k,$v)=split /=/; $dG{$k}=$v }
my %len; open L,"<$lensf"; while(<L>){ my($k,$v)=split; $len{$k}=$v }
sub i32 { my $v=shift; $v=$v & 0xFFFFFFFF; $v-=4294967296 if $v>=2147483648; $v }
open F,"<$f"; my $n=0; my @s; my $res;
while(my $l=<F>){ $n++; next if $n<$a; last if $n>$z; $l=~s/\r?\n$//;
 next unless $l=~/^\s+\d+:\s+(\S+)\s*(.*)$/; my($op,$rest)=($1,$2);
 if($op eq 'iconst_m1'){push @s,-1}
 elsif($op=~/^iconst_(\d)$/){push @s,$1+0}
 elsif($op=~/^(bipush|sipush)$/){ $rest=~/(-?\d+)/; push @s,$1+0 }
 elsif($op eq 'ldc'){ if($rest=~/\/\/ int (-?\d+)/){push @s,$1+0} elsif($rest=~/#(\d+)\s+\/\/ String/){ die "thiếu độ dài #$1" unless exists $len{$1}; push @s,{str=>$len{$1}} } else { die "ldc lạ: $l" } }
 elsif($op eq 'invokevirtual' && $rest=~/String.length/){ my $x=pop @s; push @s,$x->{str} }
 elsif($op eq 'getstatic'){ if($rest=~/Field dG:\[I/){push @s,'DG'} else { die "getstatic lạ: $l" } }
 elsif($op eq 'iaload'){ my $i=pop @s; my $r=pop @s; die "iaload lạ: $l" unless $r eq 'DG'; die "dG[$i] chưa có" unless exists $dG{$i}; push @s,$dG{$i} }
 elsif($op eq 'dup'){ push @s,$s[-1] }
 elsif($op eq 'newarray'){ my $c=pop @s; push @s,[ (0) x $c ] }
 elsif($op eq 'anewarray'){ my $c=pop @s; push @s,[ (undef) x $c ] }
 elsif($op eq 'iastore' || $op eq 'aastore'){ my $v=pop @s; my $i=pop @s; my $arr=pop @s; $arr->[$i]=$v }
 elsif($op eq 'ineg'){ my $x=pop @s; push @s,i32(-$x) }
 elsif($op=~/^(iadd|isub|imul|ixor|iand|ior|ishl|ishr|iushr)$/){ my $b=pop @s; my $x=pop @s; my $r;
   $r=$x+$b if $op eq 'iadd'; $r=$x-$b if $op eq 'isub'; $r=$x*$b if $op eq 'imul';
   $r=(i32($x)^i32($b)) if $op eq 'ixor'; $r=(i32($x)&i32($b)) if $op eq 'iand'; $r=(i32($x)|i32($b)) if $op eq 'ior';
   $r=$x<<($b&31) if $op eq 'ishl'; $r=$x>>($b&31) if $op eq 'ishr'; $r=(($x&0xFFFFFFFF)>>($b&31)) if $op eq 'iushr';
   push @s,i32($r) }
 elsif($op eq 'putstatic'){ $res=pop @s }
 else { die "lệnh lạ (dòng $n): $l" }
}
sub js { my $x=shift; return ref $x ? "[".join(",",map{js($_)}@$x)."]" : (defined $x ? $x : "null") }
print js($res),"\n";
