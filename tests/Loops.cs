class Loops {
    void loop_add_1() { for (int i=0; i<10; i++); }
    void loop_add_2() { for (int i=0; i<10; ++i); }
    void loop_add_3() { for (int i=0; i<10; i+=2); }
    void loop_add_4() { for (int i=0; i<=10; i++); }

    void loop_sub_1() { for (int i=10; i>0; i--); }
    void loop_sub_2() { for (int i=10; i>0; --i); }
    void loop_sub_3() { for (int i=10; i>0; i-=3); }
    void loop_sub_4() { for (int i=10; i>=0; i--); }
}
